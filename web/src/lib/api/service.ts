import { apiConfig } from './config';

class ApiService {
    public baseURL: string;

    constructor() {
        this.baseURL = apiConfig.baseURL;
    }

    private async request<T>(path: string, options: RequestInit = {}): Promise<T> {
        const url = `${this.baseURL}${path}`;
        const headers = new Headers(options.headers || {});
        
        // 从localStorage获取API密钥
        const apiKey = localStorage.getItem('apiKey');
        if (apiKey) {
            headers.set('X-Api-Key', apiKey);
        }

        const config: RequestInit = {
            ...options,
            headers
        };

        try {
            const response = await fetch(url, config);
            
            if (!response.ok) {
                // console.log(await response.json())
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            // 检查响应内容类型
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                const data = await response.json();
                return data as T;
            }

            // 对于没有响应体或非JSON响应，返回空对象
            return {} as T;
        } catch (error) {
            throw this.handleError(error);
        }
    }

    private handleError(error: Error | unknown): Error {
        let message = '请求失败';
        
        if (error instanceof Error) {
            if (error.message.includes('status: 401')) {
                message = 'API密码无效，请检查设置';
            } else if (error.message.includes('Failed to fetch')) {
                message = '网络连接失败，请检查网络设置';
            } else {
                message = error.message;
            }
        }

        return new Error(message);
    }

    // 文件列表
    async getFiles(currentPath:string,searchQuery:string): Promise<Array<{ name: string; size: number; type:string,lastModified: string }>> {
        return this.request(`/file/list?path=${currentPath}&searchQuery=${searchQuery}`);
    }
    async createFolder(folderName: string): Promise<{ message: string }> {
        return this.request(`/file/directory`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                path: folderName
            })
        });
    }

    // 上传文件
    async uploadFile(path:string,file: File): Promise<{ fileName: string }> {
        const formData = new FormData();
        formData.append('file', file);

        return this.request(`/file/upload?path=${path}`, {
            method: 'POST',
            body: formData
        });
    }
    // 上传大文件
    async uploadLargeFile(path: string, file: File,setPrgress:(progress:number)=>void,setIsPaused: (isPaused: boolean) => void): Promise<{ fileName: string }> {
        const CHUNK_SIZE = 3 * 1024 * 1024; // 5MB per chunk
        const totalSize = file.size;
        const totalChunks = Math.ceil(totalSize / CHUNK_SIZE);

        // 用于控制上传状态
        let isUploading = true;
        let isPaused = false;
        // 初始化上传
        const initResponse = await this.request<{ uploadId: string }>('/file/upload/large/init', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                directory: path,
                fileName: file.name,
                totalSize: totalSize,
                chunkSize: CHUNK_SIZE
            })
        });

        const uploadId = initResponse.uploadId;
        let uploadedChunks = new Set<number>();

        // 暂停上传
        const pauseUpload = async () => {
            isPaused = true;
            setIsPaused(true);
            await this.request(`/file/upload/large/pause/${uploadId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(true)
            });
        };

        // 恢复上传
        const resumeUpload = async () => {
            isPaused = false;
            setIsPaused(false);
            await this.request(`/file/upload/large/pause/${uploadId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(false)
            });
        };

        // 停止上传
        const stopUpload = async () => {
            isUploading = false;
            await this.request(`/file/upload/large/stop/${uploadId}`, {
                method: 'POST'
            });
            throw new Error('上传已停止');
        };

        // 暴露控制方法到外部
        const controller = {
            pause: pauseUpload,
            resume: resumeUpload,
            stop: stopUpload
        };
        // 暴露控制器到全局
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore
        window.uploadController = controller;
        // 定期检查上传状态
        const checkStatus = async () => {
            const status = await this.request<{
                uploadedChunks: number[],
                status: string,
                fileName?: string
            }>(`/file/upload/large/status/${uploadId}`);

            if (status.status === 'completed' && status.fileName) {
                return { completed: true, fileName: status.fileName };
            }

            uploadedChunks = new Set(status.uploadedChunks);
            setPrgress(uploadedChunks.size/totalChunks)// 更新上传进度
            return { completed: false };
        };

        // 上传单个分片
        const uploadChunk = async (chunkNumber: number): Promise<void> => {
            if (uploadedChunks.has(chunkNumber)) return;

            const start = chunkNumber * CHUNK_SIZE;
            const end = Math.min(start + CHUNK_SIZE, totalSize);
            const chunk = file.slice(start, end);

            const formData = new FormData();
            formData.append('chunk', chunk);
            formData.append('chunkNumber', chunkNumber.toString());

            await this.request(`/file/upload/large/chunk/${uploadId}`, {
                method: 'POST',
                body: formData
            });
        };

        // 并发上传分片，限制最大并发数
        const MAX_CONCURRENT_UPLOADS = 3;
        const uploadChunks = async () => {
            let retryCount = 0;
            const maxRetries = 3;
            
            while (retryCount < maxRetries&&isUploading) {
                try {
                    for (let i = 0; i < totalChunks; i += MAX_CONCURRENT_UPLOADS) {
                        // 检查是否暂停
                        while (isPaused && isUploading) {
                            await new Promise(resolve => setTimeout(resolve, 1000));
                        }
                        if (!isUploading) break;
                        const chunkPromises = [];
                        for (let j = 0; j < MAX_CONCURRENT_UPLOADS && i + j < totalChunks; j++) {
                            if (!uploadedChunks.has(i + j)) {
                                chunkPromises.push(uploadChunk(i + j));
                            }
                        }
                        if (chunkPromises.length > 0) {
                            await Promise.all(chunkPromises);
                            // 更新上传进度
                            uploadedChunks = new Set([...uploadedChunks, ...Array.from({length: MAX_CONCURRENT_UPLOADS}, (_, j) => i + j).filter(n => n < totalChunks)]);
                            setPrgress(uploadedChunks.size/totalChunks);

                            // 如果所有分片都已上传，直接返回成功
                            if (uploadedChunks.size === totalChunks) {
                                setPrgress(1); // 设置进度为100%
                                return { fileName: file.name };
                            }

                            // 检查服务器端状态
                            const status = await checkStatus();
                            if (status.completed) {
                                setPrgress(1); // 设置进度为100%
                                return { fileName: status.fileName };
                            }
                        }
                    }
                    return { fileName: file.name };
                } catch (error) {
                    if (!isUploading) throw new Error('上传已停止');
                    
                    retryCount++;
                    if (retryCount >= maxRetries) {
                        throw error;
                    }
                    await new Promise(resolve => setTimeout(resolve, 1000 * retryCount));
                }
            }
        };

        // 开始上传并等待完成
        const result = await uploadChunks();
        if (!result?.fileName) {
            throw new Error('上传失败');
        }

        return { fileName: result.fileName };
    }
    // 重命名文件
    async renameFile(pathWithOldName: string, pathWithNewName: string): Promise<{ message: string }> {
        return this.request(`/file/rename`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                pathWithOldName,
                pathWithNewName
            })
        });
    }

    // 删除文件夹
    async deleteFolder(folderPath: string): Promise<void> {
        return this.request(`/file/directory/${folderPath}`, {
            method: 'DELETE'
        });
    }

    // 删除文件
    async deleteFile(fileName: string): Promise<void> {
        return this.request(`/file/${fileName}`, {
            method: 'DELETE'
        });
    }

    // 下载文件
    async downloadFile(fileName: string, onProgress?: (progress: number) => void): Promise<Blob> {
        const url = `${this.baseURL}/file/download/${fileName}`;
        const headers = new Headers();
        const apiKey = localStorage.getItem('apiKey');
        
        if (apiKey) {
            headers.set('X-Api-Key', apiKey);
        }
    
        const response = await fetch(url, { headers });
        if (!response.ok) {
            throw this.handleError(new Error(`HTTP error! status: ${response.status}`));
        }
    
        // 获取文件大小
        const contentLength = response.headers.get('Content-Length');
        const totalSize = contentLength ? parseInt(contentLength, 10) : 0;
    
        // 如果文件小于10MB或没有进度回调，直接返回blob
        if (totalSize <= 10 * 1024 * 1024 || !onProgress) {
            return response.blob();
        }
    
        // 大文件下载处理
        // 检查response.body是否为null
        if (!response.body) {
            throw new Error('Response body is null');
        }
        const reader = response.body.getReader();
        let receivedLength = 0;
        const chunks: Uint8Array[] = [];
    
        while (true) {
            const { done, value } = await reader.read();
    
            if (done) {
                break;
            }
    
            chunks.push(value);
            receivedLength += value.length;
            onProgress(receivedLength / totalSize);
        }
    
        // 合并所有chunks
        const chunksAll = new Uint8Array(receivedLength);
        let position = 0;
        for (const chunk of chunks) {
            chunksAll.set(chunk, position);
            position += chunk.length;
        }
    
        return new Blob([chunksAll]);
    }

    async moveFile(oldPath: string, newPath: string): Promise<void> {
        return this.request(`/file/move`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                SourcePath:oldPath,
                DestinationPath:newPath
            })
        });
    }

    // 测试API密钥
    async testApiKey(): Promise<boolean> {
        try {
            await this.request('/file/test');
            return true;
        } catch {
            return false;
        }
    }
}

export const apiService = new ApiService();