<script lang="ts">
  import { onMount } from "svelte";
  import { apiService } from "$lib/api/service";
  import { showToast } from "$lib/components/toastSstore";
  import FileList from "$lib/components/FileList.svelte";
    import PathNavigator from "$lib/components/PathNavigator.svelte";
    import UploadProgress from "$lib/components/UploadProgress.svelte";

  type Item = {
    name: string;
    type: string;
    size: number;
    lastModified: string;
  };
  let explorerPersistStatus = $state<{
    currentPath: string;
    items: Item[];
    searchQuery: string;
    sortField: string;
    sortDirection: string;
    apiKey: string;
  }>({
    currentPath: "",
    items: [],
    searchQuery: "",
    sortField: "name",
    sortDirection: "asc",
    apiKey: "your_secure_password",
  });
  let explorerInstantStatus = $state({
    dragActive: false,
    uploadProgress: 0,
    isPaused: false,
    isUploading: false,
    showSettingsModal: false,
    showMobileSettingModal:false
  });
  let apiKeyTestStatus = $state({
    testingApiKey: false,
    testSuccess: false,
    showTestResult: false,
  });

  async function handleCreateFolder() {
    const folderName = prompt(
      `您要在当前文件夹${explorerPersistStatus.currentPath ?? "root"}下创建文件夹，请输入文件夹名称`,
    );
    if (folderName) {
      // 检查文件夹名称是否包含非法字符
      const invalidChars = /[\\/:*?"<>|]/;
      if (invalidChars.test(folderName)) {
        showToast("文件夹名称不能包含以下字符：\\ / : * ? \" < > |", "error");
        return;
      }
      try {
        await apiService.createFolder(
          explorerPersistStatus.currentPath
            ? `${explorerPersistStatus.currentPath}/${folderName}`
            : folderName,
        );
        showToast("文件夹创建成功", "success");
      } catch (error) {
        showToast(
          error instanceof Error ? error.message : "创建文件夹失败",
          "error",
        );
      } finally {
        await loadFiles();
      }
    }
  }

  // 检查文件是否为浏览器可直接打开的格式
  function isBrowserViewableFile(fileName: string): boolean {
    if (!fileName) return false;
    
    const extension = fileName.toLowerCase().split('.').pop() || '';
    
    const viewableExtensions = [
      // 图片格式
      'jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'svg',
      // 文本格式
      'txt', 'md', 'json', 'xml', 'html', 'htm', 'css', 'js', 'ts', 'log',
      // 文档格式
      'pdf',
      // 音频格式
      'mp3', 'wav', 'ogg', 'm4a',
      // 视频格式
      'mp4', 'webm', 'ogv'
    ];
    
    return viewableExtensions.includes(extension);
  }

  async function openItem(itemName: string) {
    try {
      const item = explorerPersistStatus.items.find((f) => f.name === itemName);
      if (item?.type === "directory") {
        explorerPersistStatus.currentPath = explorerPersistStatus.currentPath
          ? `${explorerPersistStatus.currentPath}/${itemName}`
          : itemName;
        await loadFiles();
        return;
      }
      
      const blob = await apiService.downloadFile(
        explorerPersistStatus.currentPath
          ? `${explorerPersistStatus.currentPath}/${itemName}`
          : itemName,
      );

      // 只有浏览器可直接打开的文件格式才会在新标签页中打开
      if (item?.name && isBrowserViewableFile(item.name)) {
        const url = window.URL.createObjectURL(blob);
        window.open(url, "_blank");
        window.URL.revokeObjectURL(url);
      } else {
        // 对于不支持的文件格式，提示用户下载
        showToast("此文件格式不支持直接预览，请使用下载功能", "info");
        return;
      }

    } catch (error) {
      showToast(
        error instanceof Error ? error.message : "文件打开失败",
        "error",
      );
    }
  }

  async function deleteItem(fileName: string) {
    if (
      !confirm(
        `确定要删除${explorerPersistStatus.items.find((f) => f.name === fileName)?.type === "directory" ? "文件夹" : "文件"} "${fileName}" 吗？`,
      )
    ) {
      return;
    }
    try {
      const item = explorerPersistStatus.items.find((f) => f.name === fileName);
      if (item?.type === "directory") {
        await apiService.deleteFolder(
          explorerPersistStatus.currentPath
            ? `${explorerPersistStatus.currentPath}/${fileName}`
            : fileName
        );
      } else {
        await apiService.deleteFile(
          explorerPersistStatus.currentPath
            ? `${explorerPersistStatus.currentPath}/${fileName}`
            : fileName
        );
      }
      await loadFiles();
      showToast("删除成功", "success");
    } catch (error) {
      showToast(error instanceof Error ? error.message : "删除失败", "error");
    }
  }

  async function downloadFile(fileName: string) {
    try {
      const blob = await apiService.downloadFile(
        explorerPersistStatus.currentPath
          ? `${explorerPersistStatus.currentPath}/${fileName}`
          : fileName,
      );
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      showToast("文件下载成功", "success");
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : "文件下载失败",
        "error",
      );
    }
  }
  function handleClickOutside(event: MouseEvent) {
        if (explorerInstantStatus.showMobileSettingModal) {
            const target = event.target as HTMLElement;
            const dropdown = document.getElementById('mobile-api-key');
            
            
            if (dropdown &&
                !dropdown.contains(target) )
                 {
                explorerInstantStatus.showMobileSettingModal=false
            }
        }
    }
  onMount(() => {
    const savedApiKey = localStorage.getItem("apiKey");
    if (savedApiKey) {
      explorerPersistStatus.apiKey = savedApiKey;
    }
    loadFiles();
    document.addEventListener('click', handleClickOutside, true);
    return ()=>document.removeEventListener('click', handleClickOutside, true);
  });

  function saveApiKey() {
    localStorage.setItem("apiKey", explorerPersistStatus.apiKey);
    explorerInstantStatus.showSettingsModal = false;
    showToast("API密码已保存", "success");
  }

  async function testApiKey() {
    apiKeyTestStatus.testingApiKey = true;
    apiKeyTestStatus.showTestResult = false;
    localStorage.setItem("apiKey", explorerPersistStatus.apiKey);
    try {
      apiKeyTestStatus.testSuccess = await apiService.testApiKey();
      apiKeyTestStatus.showTestResult = true;
      if (apiKeyTestStatus.testSuccess) {
        showToast("API密码验证成功", "success");
      } else {
        showToast("API密码验证失败", "error");
      }
    } catch (error) {
      apiKeyTestStatus.testSuccess = false;
      apiKeyTestStatus.showTestResult = true;
      showToast("API密码验证失败", "error");
    } finally {
      apiKeyTestStatus.testingApiKey = false;
    }
  }

  async function loadFiles() {
    try {
      explorerPersistStatus.items = await apiService.getFiles(
        explorerPersistStatus.currentPath,
        explorerPersistStatus.searchQuery,
      );
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : "加载文件列表失败",
        "error",
      );
    }
  }

  async function handleFileUpload(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) {
      await uploadFile(input.files[0]);
    }
  }
  async function uploadFile(file:File) {
    if(file.size>1024*1024*10){
      await uploadLargeFile(file)
    }else{
      await uploadSmallFile(file)
    }
  }
  
  async function uploadLargeFile(file:File) {
    explorerInstantStatus.isUploading = true;
    explorerInstantStatus.uploadProgress = 0;
    try{

      await apiService.uploadLargeFile(explorerPersistStatus.currentPath, file, (progress) => {
        explorerInstantStatus.uploadProgress = Number((progress * 100).toFixed(1));
      },()=>explorerInstantStatus.isPaused=true);
    }catch(error){
      showToast(
        error instanceof Error? error.message : "文件上传失败",
        "error",
      );
    }finally{
      setTimeout(() => {
        
        explorerInstantStatus.isUploading = false;
        explorerInstantStatus.uploadProgress = 0;
      }, 1500);
        await loadFiles();
    }
  }

  async function uploadSmallFile(file: File) {
    explorerInstantStatus.isUploading = true;
    explorerInstantStatus.uploadProgress = 0;

    // 创建模拟进度的定时器
    let progressInterval = setInterval(() => {
      if (explorerInstantStatus.uploadProgress < 85) {
        explorerInstantStatus.uploadProgress += (85 - explorerInstantStatus.uploadProgress) * 0.1;
      }
    }, 100);

    try {
      await apiService.uploadFile(explorerPersistStatus.currentPath, file);
      
      // 上传完成后，平滑过渡到100%
      clearInterval(progressInterval);
      const finalizeProgress = setInterval(() => {
        if (explorerInstantStatus.uploadProgress >= 99.9) {
          clearInterval(finalizeProgress);
          explorerInstantStatus.uploadProgress = 100;
        } else {
          explorerInstantStatus.uploadProgress += (100 - explorerInstantStatus.uploadProgress) * 0.2;
        }
      }, 50);

      await loadFiles();
      
      // 等待进度条动画完成后再显示成功提示
      setTimeout(() => {
        showToast("文件上传成功", "success");
        explorerInstantStatus.isUploading = false;
        explorerInstantStatus.uploadProgress = 0;
      }, 800);
    } catch (error) {
      clearInterval(progressInterval);
      explorerInstantStatus.uploadProgress = 0;
      showToast(
        error instanceof Error ? error.message : "文件上传失败",
        "error",
      );
      explorerInstantStatus.isUploading = false;
    }
  }

  function handleDragEnter(e: DragEvent) {
    e.preventDefault();
    explorerInstantStatus.dragActive = true;
  }

  function handleDragLeave(e: DragEvent) {
    e.preventDefault();
    explorerInstantStatus.dragActive = false;
  }

  function handleDrop(e: DragEvent) {
    e.preventDefault();
    explorerInstantStatus.dragActive = false;

    if (e.dataTransfer?.files.length) {
      uploadSmallFile(e.dataTransfer.files[0]);
    }
  }

  function formatFileSize(bytes: number): string {
    if (bytes === 0) return "0 Bytes";
    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  }
  async function renameItem(oldName: string) {
    const newName = prompt("请输入新的文件名", oldName);
    if (newName) {
      try {
        await apiService.renameFile(
          explorerPersistStatus.currentPath
           ? `${explorerPersistStatus.currentPath}/${oldName}`
            : oldName,
            explorerPersistStatus.currentPath
           ? `${explorerPersistStatus.currentPath}/${newName}`
            : newName,
        );
        await loadFiles();
      } catch (error) {
        showToast(
          error instanceof Error? error.message : "重命名失败",
          "error",
        );
      }
    }
  }

  async function moveItem(sourceItemName: string, targetFolderName: string) {
    try {
      console.log(sourceItemName, targetFolderName);
      const sourcePath = explorerPersistStatus.currentPath
        ? `${explorerPersistStatus.currentPath}/${sourceItemName}`
        : sourceItemName;
      const targetPath = explorerPersistStatus.currentPath
        ? `${explorerPersistStatus.currentPath}/${targetFolderName}`
        : targetFolderName;

      await apiService.moveFile(sourcePath, `${targetPath}/${sourceItemName}`);
      await loadFiles();
      showToast("文件移动成功", "success");
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : "文件移动失败",
        "error"
      );
    }
  }

  

</script>

<div class="container mx-auto px-4 py-4 sm:py-8 relative max-w-7xl h-screen flex flex-col">
  <div class="flex justify-between items-center mb-6 sm:mb-8">
    <!-- <div class="hidden sm:block"></div> -->
    <div class="text-lg sm:text-xl md:text-2xl font-bold">
      <svg class="h-10 w-10" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" width="200" height="200"><path d="M828.728889 286.606222A89.770667 89.770667 0 0 0 738.986667 196.835556H285.013333a89.770667 89.770667 0 0 0-89.6 84.679111l-0.142222 5.091555v412.700445a89.770667 89.770667 0 0 0 84.650667 89.6l5.12 0.142222H738.986667a89.770667 89.770667 0 0 0 89.6-84.650667l0.142222-5.12v-18.545778l0.199111-3.470222a34.048 34.048 0 0 1 30.378667-30.378666l3.470222-0.170667c18.801778 0 34.048 15.246222 34.048 34.048v18.545778a157.866667 157.866667 0 0 1-151.637333 157.724444l-6.200889 0.113778h-38.712889l23.836444 41.244444a34.048 34.048 0 0 1-58.965333 34.076445l-40.618667-70.371556a34.304 34.304 0 0 1-2.360889-4.949333H422.968889c-0.625778 1.706667-1.422222 3.356444-2.360889 4.949333l-40.618667 70.371556a34.048 34.048 0 0 1-58.965333-34.048l23.836444-41.301334-59.847111 0.028445a157.866667 157.866667 0 0 1-157.724444-151.665778l-0.113778-6.172444v-412.728889a157.866667 157.866667 0 0 1 151.637333-157.696l6.200889-0.142222h453.973334a157.866667 157.866667 0 0 1 157.724444 151.665777l0.113778 6.200889v234.609778a34.048 34.048 0 0 1-68.096 0v-234.609778z" fill="#68B8F7"></path><path d="M737.735111 574.72a34.048 34.048 0 1 1 0 68.067556H286.264889a34.048 34.048 0 1 1 0-68.067556h451.470222z m0-201.073778a34.048 34.048 0 1 1 0 68.096H286.264889a34.048 34.048 0 1 1 0-68.096h451.470222z" fill="#68B8F7" opacity=".6"></path></svg>
    </div>
    <div class="flex gap-2 w-40 md:w-48 lg:w-64">
      <button aria-label="返回上一级"
        onclick={() => {
          if (explorerPersistStatus.currentPath) {
            explorerPersistStatus.currentPath =
              explorerPersistStatus.currentPath
                .split("/")
                .slice(0, -1)
                .join("/");
            loadFiles();
          }
        }}
        disabled={!explorerPersistStatus.currentPath}
        class="p-1.5 text-gray-600 hover:text-gray-800 transition-colors rounded-lg hover:bg-gray-100 disabled:opacity-50 disabled:hover:bg-transparent disabled:cursor-not-allowed"
        title="返回上级目录"
      >
      <svg class="h-6 w-6 fill-slate-600" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" width="200" height="200"><path d="M212.62 344.62c7.06-4.78 12.6 0.06 34.11 0.15 65.56 0.3 309.55 1.37 387.61 1.71 13.41 0.06 24.37-10.67 24.61-24.08l0.81-45.44c0.21-11.88-9.19-21.72-21.07-22.05-24.83-0.68-63.31-1.88-71.07-3.02-33.93-4.97-40.38-26.09-59.98-45.66-19.6-19.57-44.73-28.28-44.73-28.28l-260.04 0.39s-32.7 0.31-57.9 14.35c-31.31 17.45-52.29 96.55-56.1 132.26-8.86 83.03 0 334 0 334l110.59-280.9c-0.01 0.01 5.72-28.39 13.16-33.43z m708.91 231.43H678.79c-10.33 0-18.74-8.32-18.85-18.66l-1.17-114.37c-0.14-13.78-11.34-24.87-25.12-24.87-74.86-0.01-294.31-0.02-315.81 0.32-52.32 0.82-62.23 40.88-62.23 40.88S137.13 768.72 123.43 802.48c-13.7 33.76 15 37.19 15 37.19h648.06c56.93 0 78.56-43.71 78.56-43.71l69.38-201.83c3.05-8.86-3.53-18.08-12.9-18.08z m-251.36-354.3h40.27c3.93 0 7.1 3.2 7.06 7.13l-2.72 307.59c-0.03 3.95 3.17 7.16 7.12 7.13l179.01-1.35a7.058 7.058 0 0 0 7.01-6.95l0.8-49.94c0.06-3.94-3.12-7.18-7.06-7.18H789.62c-3.9 0-7.06-3.16-7.06-7.06V226.94c0-3.9 3.16-7.06 7.06-7.06h54.8c6.57 0 9.58-8.19 4.57-12.45l-88.73-75.31a7.061 7.061 0 0 0-9.3 0.14l-85.51 77.18c-4.81 4.33-1.75 12.31 4.72 12.31z" ></path></svg>
      </button>
      
      <PathNavigator
            currentPath={explorerPersistStatus.currentPath}
            onNavigate={(path) => {
              explorerPersistStatus.currentPath = path;
              loadFiles();
            }}
          />
          <button aria-label="刷新"
        onclick={loadFiles}
        class="p-1.5 text-gray-600 hover:text-gray-800 transition-colors rounded-lg hover:bg-gray-100"
        title="刷新"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          class="h-4 w-4"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
          />
        </svg>
      </button>
    </div>
    
    <button aria-label="showApiKeySettings"
      onclick={() => (explorerInstantStatus.showSettingsModal = true)}
      class="p-2 hidden sm:block text-gray-600 rounded-md hover:bg-gray-100 transition-colors"
      title="API密码设置"
    >
      <svg
        xmlns="http://www.w3.org/2000/svg"
        class="h-5 w-5 sm:h-6 sm:w-6"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
      >
        <path
          stroke-linecap="round"
          stroke-linejoin="round"
          stroke-width="2"
          d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"
        />
      </svg>
    </button>
    <div class="relative sm:hidden" id="mobile-api-key">
    <button aria-label="showMobileSetting" onclick={()=>explorerInstantStatus.showMobileSettingModal=!explorerInstantStatus.showMobileSettingModal} >
      <svg  class="h-6 w-6 fill-slate-500" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" width="200" height="200"><path d="M415.93 223.79c0-52.98 43.004-95.984 95.984-95.984s95.984 43.004 95.984 95.984-43.004 95.984-95.984 95.984-95.984-43.003-95.984-95.984zM415.93 511.742c0-52.98 43.004-95.984 95.984-95.984s95.984 43.004 95.984 95.984-43.004 95.984-95.984 95.984-95.984-43.004-95.984-95.984zM415.93 799.866c0-52.98 43.004-95.984 95.984-95.984s95.984 43.003 95.984 95.984-43.004 95.983-95.984 95.983-95.984-43.175-95.984-95.983z" fill="#68B8F7"></path></svg>
    </button>
    {#if explorerInstantStatus.showMobileSettingModal}
    <button aria-label="showApiKeySettings"
      onclick={() => (explorerInstantStatus.showSettingsModal = true)}
      class="p-2 absolute right-0 top-4 bg-slate-100 flex items-center hover:bg-slate-200 justify-center rounded-md w-36 h-10 gap-2 text-amber-400 hover:text-gray-800 transition-colors"
      title="API密码设置"
    >
      <svg
        xmlns="http://www.w3.org/2000/svg"
        class="h-5 w-5 sm:h-6 sm:w-6"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
      >
        <path
          stroke-linecap="round"
          stroke-linejoin="round"
          stroke-width="2"
          d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"
        />
      </svg>Set api Key
    </button>
    {/if}
    </div>

    
  </div>

  {#if explorerInstantStatus.showSettingsModal}
    <div
      class="fixed inset-0 bg-slate-600 bg-opacity-85 flex items-center justify-center z-50 p-4"
    >
      <div class="bg-slate-50 rounded-lg p-4 sm:p-6 w-full max-w-md">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-lg sm:text-xl font-semibold">API密码设置</h2>
          <button aria-label="closeModal"
            onclick={() => (explorerInstantStatus.showSettingsModal = false)}
            class="text-gray-500 hover:text-gray-700 p-1"
          >
            <svg
              xmlns="http://www.w3.org/2000/svg"
              class="h-5 w-5 sm:h-6 sm:w-6"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="2"
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>
        <div class="space-y-4">
          <div class="relative">
            <input
              type="password"
              bind:value={explorerPersistStatus.apiKey}
              placeholder="请输入API密码"
              class="w-full px-3 sm:px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-base"
            />
            <button
              onclick={testApiKey}
              disabled={apiKeyTestStatus.testingApiKey}
              class="absolute right-2 top-1/2 -translate-y-1/2 px-2 sm:px-3 py-1 text-sm bg-blue-500 text-white rounded hover:bg-blue-600 disabled:bg-blue-300 transition-colors"
            >
              {#if apiKeyTestStatus.testingApiKey}
                测试中...
              {:else}
                测试
              {/if}
            </button>
          </div>
          {#if apiKeyTestStatus.showTestResult}
            <div
              class="text-sm {apiKeyTestStatus.testSuccess
                ? 'text-green-500'
                : 'text-red-500'} flex items-center gap-2"
            >
              {#if apiKeyTestStatus.testSuccess}
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="h-4 w-4 sm:h-5 sm:w-5"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M5 13l4 4L19 7"
                  />
                </svg>
                <span>API密码验证成功</span>
              {:else}
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="h-4 w-4 sm:h-5 sm:w-5"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
                <span>API密码验证失败</span>
              {/if}
            </div>
          {/if}
          <button
            onclick={saveApiKey}
            class="w-full px-4 py-2 bg-green-500 text-white rounded-lg hover:bg-green-600 transition-colors text-base"
          >
            保存密码
          </button>
        </div>
      </div>
    </div>
  {/if}

  <!-- 上传区域 -->
  <div
    role="region"
    aria-label="文件上传区域"
    class="hidden relative lg:block mb-6 sm:mb-8 border-2 border-dashed rounded-lg p-4 sm:p-8 text-center"
    class:border-blue-500={explorerInstantStatus.dragActive}
    ondragenter={handleDragEnter}
    ondragleave={handleDragLeave}
    ondragover={e=>e.preventDefault()}
    ondrop={handleDrop}
  >
    <input
      type="file"
      id="fileInput"
      class="hidden"
      onchange={handleFileUpload}
    />
    <label
      for="fileInput"
      class="cursor-pointer inline-block px-4 sm:px-6 py-2 sm:py-3 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors text-sm sm:text-base"
    >
      选择文件上传
    </label>
    <p class="mt-2 text-gray-600 text-sm sm:text-base">或将文件拖放到此处</p>

    <div class="absolute bottom-1 right-1 left-1">

      <UploadProgress
      progress={explorerInstantStatus.uploadProgress}
      isUploading={explorerInstantStatus.isUploading}
      isPaused={explorerInstantStatus.isPaused}
      />
    </div>
  </div>
  
  <!-- 文件列表 -->
  <div class="bg-white rounded-lg shadow overflow-hidden flex-1 flex flex-col min-h-0">
    <div class="p-4 border-b border-gray-200">
      <div class="flex flex-wrap gap-4 items-center">
        <div class="flex-1 min-w-[200px] flex items-center gap-2">
          <!-- <span
            class="hidden sm:block text-gray-600 bg-gray-200 w-24 px-3 py-1 rounded-md text-center truncate text-sm whitespace-nowrap"
            >{explorerPersistStatus.currentPath === ""
              ? "root"
              : explorerPersistStatus.currentPath}</span
          > -->
          
          <div class="relative flex-1">
            <input
              type="text"
              bind:value={explorerPersistStatus.searchQuery}
              placeholder="搜索文件..."
              class="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm pr-8"
            />
            {#if explorerPersistStatus.searchQuery}
              <button
                class="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition-colors"
                onclick={() => {
                  explorerPersistStatus.searchQuery = '';
                  loadFiles();
                }}
                aria-label="清空搜索"
              >
                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            {/if}
          </div>
        </div>
        <div class="flex gap-2">
          <button aria-label="create_folder"
            onclick={handleCreateFolder}
            class="px-2 py-2 rounded-lg text-gray-700 hover:bg-gray-50 text-sm flex items-center gap-2"
          >
          <svg class="h-6 w-6 fill-slate-500" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg"  width="200" height="200"><path d="M983.04 226.56c-25.6-25.6-61.44-40.96-98.56-40.96H490.24l-79.36-119.04c-8.96-12.8-23.04-20.48-38.4-20.48H139.52C62.72 46.08 0 108.8 0 185.6v651.52c0 76.8 62.72 139.52 139.52 139.52h744.96c76.8 0 139.52-62.72 139.52-139.52v-512c0-35.84-14.08-71.68-40.96-98.56z m-52.48 611.84c0 25.6-20.48 46.08-46.08 46.08H139.52c-25.6 0-46.08-20.48-46.08-46.08v-652.8c0-25.6 20.48-46.08 46.08-46.08h207.36l79.36 119.04c8.96 12.8 23.04 20.48 38.4 20.48H883.2c25.6 0 46.08 20.48 46.08 46.08v513.28z" ></path><path d="M651.52 552.96h-93.44v-93.44c0-25.6-20.48-46.08-46.08-46.08h-5.12c-23.04 2.56-40.96 23.04-40.96 46.08v93.44h-93.44c-25.6 0-46.08 20.48-46.08 46.08v5.12c2.56 23.04 23.04 40.96 46.08 40.96h93.44v93.44c0 25.6 20.48 46.08 46.08 46.08h5.12c23.04-2.56 40.96-23.04 40.96-46.08v-93.44h93.44c25.6 0 46.08-20.48 46.08-46.08v-5.12c-2.56-24.32-21.76-40.96-46.08-40.96z"></path></svg>
          </button>
          <div class="flex lg:hidden px-1 hover:bg-slate-100 rounded-md items-center justify-center">
            <input id="mobile_file" onchange={handleFileUpload} type="file" class="hidden"/>
            <label for="mobile_file">

              <svg class="h-7 w-7 fill-slate-500" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" width="200" height="200"><path d="M512 402.285714l146.285714 182.857143h-109.714285v256h-73.142858v-256h-109.714285l146.285714-182.857143z m0-256a256.036571 256.036571 0 0 1 254.171429 225.389715 237.750857 237.750857 0 0 1-44.507429 469.321142L713.142857 841.142857H658.285714v-73.142857h53.577143l7.241143-0.109714a164.571429 164.571429 0 0 0 38.436571-322.962286l-7.570285-1.938286-50.322286-11.446857-6.070857-51.2a182.893714 182.893714 0 0 0-361.947429-8.301714l-1.170285 8.265143-6.070858 51.2-50.285714 11.446857a164.571429 164.571429 0 0 0 29.037714 324.864L310.857143 768H365.714286v73.142857H310.857143a237.714286 237.714286 0 0 1-53.028572-469.504A256 256 0 0 1 512 146.285714z"></path></svg>
            </label>
            </div>
          </div>
        </div>
      </div>
      <div class="relative pt-4">
        <div class="w-full lg:hidden px-4">

          <UploadProgress
          progress={explorerInstantStatus.uploadProgress}
          isUploading={explorerInstantStatus.isUploading}
          isPaused={explorerInstantStatus.isPaused}
          />
        </div>
        <FileList
        items={explorerPersistStatus.items}
        searchQuery={explorerPersistStatus.searchQuery}
        onOpenItem={openItem}
        onDownloadFile={downloadFile}
        onDeleteItem={deleteItem}
        onRenameItem={renameItem}
        onMoveItem={moveItem}
        />
      </div>
    </div>
</div>
