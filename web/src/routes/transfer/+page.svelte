<script lang="ts">
    import { onMount } from "svelte";
    import { apiService } from "$lib/api/service";
    import { SignalRService } from "$lib/api/signalr";
    import type { Item } from "$lib/types";

    let files: Array<{
        name: string;
        size: number;
        type: string;
        lastModified: string;
    }> = [];
    let uploadProgress = 0;
    let downloadProgress = 0;
    let isPaused = false;
    let currentPath = "";
    let searchQuery = "";

    onMount(() => {
        loadFiles();
        let signalRService = new SignalRService();
        async function initSignalR() {
            // 连接SignalR服务器
            try {
                await signalRService.start();

                // 监听传输进度
                signalRService.onTransferProgress((fileName, progress) => {
                    if (files.some((f) => f.name === fileName)) {
                        uploadProgress = progress * 100;
                    }
                });

                // 监听传输完成
                signalRService.onTransferComplete((fileName) => {
                    uploadProgress = 0;
                    console.log(fileName);
                    loadFiles(); // 重新加载文件列表
                });

                // 监听传输错误
                signalRService.onTransferError((fileName, error) => {
                    console.error(`文件 ${fileName} 传输失败:`, error);
                    uploadProgress = 0;
                });
            } catch (error) {
                console.error("连接SignalR服务器失败:", error);
            }
        }
        initSignalR();
        return () => {
            signalRService.stop();
        };
    });

    async function loadFiles() {
        try {
            files = await apiService.transferListFiles(currentPath);
        } catch (error) {
            console.error("加载文件失败:", error);
        }
    }

    async function handleFileUpload(event: Event) {
        const input = event.target as HTMLInputElement;
        if (!input.files?.length) return;
        try {
            const file = input.files[0];

            await apiService.transferUploadFile(currentPath, file);

            console.log("上传成功");
            await loadFiles(); // 重新加载文件列表
        } catch (error) {
            console.error("上传失败:", error);
        } finally {
            uploadProgress = 0;
            isPaused = false;
        }
    }

    async function handleFileDownload(fileName: string) {
        try {
            const blob = await apiService.downloadFile(fileName, (progress) => {
                downloadProgress = progress * 100;
            });

            const url = window.URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = fileName;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        } catch (error) {
            console.error("下载失败:", error);
        } finally {
            downloadProgress = 0;
        }
    }
</script>

<div class="container mx-auto p-4">
    <!-- 导航栏 -->
    <div class="w-full flex items-center justify-between bg-white p-4 rounded-lg shadow-md mb-6">
        <h1 class="text-2xl font-bold text-gray-800">文件传输助手</h1>
        <a href="/" class="px-4 py-2 bg-blue-500 text-white rounded-md hover:bg-blue-600 transition-colors duration-300 flex items-center">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 mr-1" viewBox="0 0 20 20" fill="currentColor">
                <path d="M10.707 2.293a1 1 0 00-1.414 0l-7 7a1 1 0 001.414 1.414L10 4.414l6.293 6.293a1 1 0 001.414-1.414l-7-7z" />
                <path d="M3 11a1 1 0 112 0v3a1 1 0 01-1 1h10a1 1 0 001-1v-3a1 1 0 112 0v3a3 3 0 01-3 3H5a3 3 0 01-3-3v-3z" />
            </svg>
            Home
        </a>
    </div>
         
    <!-- 上传区域 -->
    <div class="mb-6 p-4 border-2 border-dashed border-gray-300 rounded-lg">
        <input type="file" on:change={handleFileUpload} class="mb-2" />
        {#if uploadProgress > 0}
            <div
                class="w-full bg-gray-200 rounded-full h-2.5 dark:bg-gray-700 mb-2"
            >
                <div
                    class="bg-blue-600 h-2.5 rounded-full"
                    style="width: {uploadProgress}%"
                ></div>
            </div>
            <p class="text-sm text-gray-600">
                上传进度: {uploadProgress.toFixed(1)}%
            </p>
        {/if}
    </div>

    <!-- 文件列表 -->
    <div class="bg-white rounded-lg shadow">
        <div class="p-4">
            <h2 class="text-xl font-semibold mb-4">文件列表</h2>
            <div class="space-y-2">
                {#each files as file}
                    <div
                        class="flex items-center justify-between p-3 bg-gray-50 rounded"
                    >
                        <div>
                            <p class="font-medium">{file.name}</p>
                            <p class="text-sm text-gray-500">
                                大小: {(file.size / 1024 / 1024).toFixed(2)} MB
                            </p>
                        </div>
                        <button
                            on:click={() => handleFileDownload(file.name)}
                            class="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600"
                        >
                            下载
                        </button>
                    </div>
                {/each}
            </div>
        </div>
        {#if downloadProgress > 0}
            <div class="p-4 border-t">
                <div
                    class="w-full bg-gray-200 rounded-full h-2.5 dark:bg-gray-700 mb-2"
                >
                    <div
                        class="bg-green-600 h-2.5 rounded-full"
                        style="width: {downloadProgress}%"
                    ></div>
                </div>
                <p class="text-sm text-gray-600">
                    下载进度: {downloadProgress.toFixed(1)}%
                </p>
            </div>
        {/if}
    </div>
</div>
