<script lang="ts">
    import { onMount } from 'svelte';
    
    type FileInfo = {
        name: string;
        type: string;
        size: number;
        lastModified: string;
        path?: string;
    };
    
    let { isOpen, fileInfo, onClose } : {
        isOpen: boolean,
        fileInfo: FileInfo | null,
        onClose: () => void
    } = $props();
    
    let modalElement: HTMLDivElement;
    
    function handleClickOutside(event: MouseEvent) {
        if (modalElement && !modalElement.contains(event.target as Node) && isOpen) {
            onClose();
        }
    }
    
    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'Escape' && isOpen) {
            onClose();
        }
    }
    
    function formatFileSize(bytes: number, isDirectory: boolean): string {
        if (isDirectory) return '-';
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }
    
    function formatDate(dateString: string): string {
        const date = new Date(dateString);
        return date.toLocaleString('zh-CN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        });
    }
    
    function getFileTypeDescription(type: string): string {
        if (type === 'directory') return '文件夹';
        
        const mimeType = type.toLowerCase();
        if (mimeType.includes('image')) return '图片文件';
        if (mimeType.includes('video')) return '视频文件';
        if (mimeType.includes('audio')) return '音频文件';
        if (mimeType.includes('pdf')) return 'PDF文档';
        if (mimeType.includes('text')) return '文本文件';
        if (mimeType.includes('application/json')) return 'JSON文件';
        if (mimeType.includes('application/xml')) return 'XML文件';
        if (mimeType.includes('application/zip')) return '压缩文件';
        
        return '文件';
    }
    
    onMount(() => {
        document.addEventListener('mousedown', handleClickOutside);
        document.addEventListener('keydown', handleKeyDown);
        
        return () => {
            document.removeEventListener('mousedown', handleClickOutside);
            document.removeEventListener('keydown', handleKeyDown);
        };
    });
</script>

{#if isOpen && fileInfo}
<div class="fixed inset-0 bg-black bg-opacity-50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
    <div 
        class="bg-white rounded-lg shadow-xl w-full max-w-md overflow-hidden transform transition-all"
        bind:this={modalElement}
    >
        <div class="bg-gray-100 px-4 py-3 border-b border-gray-200 flex justify-between items-center">
            <h3 class="text-lg font-medium text-gray-900 truncate max-w-[80%]">{fileInfo.name}</h3>
            <button 
                class="text-gray-400 hover:text-gray-500 focus:outline-none"
                onclick={onClose}
            >
                <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
            </button>
        </div>
        
        <div class="px-4 py-3">
            <div class="grid grid-cols-3 gap-4 mb-4">
                <div class="col-span-1 text-gray-500 text-sm">文件类型</div>
                <div class="col-span-2 text-gray-900 text-sm font-medium">{getFileTypeDescription(fileInfo.type)}</div>
                
                <div class="col-span-1 text-gray-500 text-sm">文件大小</div>
                <div class="col-span-2 text-gray-900 text-sm font-medium">{formatFileSize(fileInfo.size, fileInfo.type === 'directory')}</div>
                
                <div class="col-span-1 text-gray-500 text-sm">修改时间</div>
                <div class="col-span-2 text-gray-900 text-sm font-medium">{formatDate(fileInfo.lastModified)}</div>
                
                {#if fileInfo.path !== undefined}
                <div class="col-span-1 text-gray-500 text-sm">文件路径</div>
                <div class="col-span-2 text-gray-900 text-sm font-medium break-all">{fileInfo.path}</div>
                {/if}
            </div>
        </div>
        
        <div class="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button 
                type="button" 
                class="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-blue-600 text-base font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 sm:ml-3 sm:w-auto sm:text-sm"
                onclick={onClose}
            >
                关闭
            </button>
        </div>
    </div>
</div>
{/if}