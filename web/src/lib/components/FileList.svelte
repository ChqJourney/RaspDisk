<script lang="ts">
    import { onMount } from 'svelte';
    import FileInfoModal from './FileInfoModal.svelte';
    type Item = {
        name: string;
        type: string;
        size: number;
        lastModified: string;
    };
    let {items,onOpenItem,onDownloadFile,onDeleteItem,onRenameItem,onMoveItem,searchQuery}:{
        items:Item[],
        onOpenItem:(fileName: string) => void,
        onDownloadFile:(fileName: string) => void,
        onDeleteItem:(fileName: string) => void,
        onRenameItem:(oldItemName:string)=>void,
        onMoveItem:(sourceItemName: string, targetFolderName: string) => void,
        searchQuery:string} = $props();

    let selectedIndex = $state(0);
    let listElement: HTMLDivElement;
    let dropdownOpen = $state(false);
    let dropdownIndex=$state(0);
    let draggedItem = $state<string | null>(null);
    let clipboardItem = $state<{action: 'copy' | 'cut', name: string} | null>(null);


    function handleDragStart(event: DragEvent, itemName: string) {
        event.dataTransfer?.setData('text/plain', itemName);
        draggedItem = itemName;
    }

    function handleDragOver(event: DragEvent, targetItem: Item) {
        if (targetItem.type === 'directory' && draggedItem && draggedItem !== targetItem.name) {
            event.preventDefault();
            (event.currentTarget as HTMLElement)?.classList.add('bg-blue-100');
        }
    }

    function handleDragLeave(event: DragEvent) {
        (event.currentTarget as HTMLElement).classList.remove('bg-blue-100');
    }

    function handleDrop(event: DragEvent, targetItem: Item) {
        event.preventDefault();
        (event.currentTarget as HTMLElement).classList.remove('bg-blue-100');
        if (targetItem.type === 'directory' && draggedItem && draggedItem !== targetItem.name) {
            onMoveItem(draggedItem, targetItem.name);
        }
        draggedItem = null;
    }

    function handleCopy(itemName: string) {
        clipboardItem = { action: 'copy', name: itemName };
        closeDropdown();
    }

    function handleCut(itemName: string) {
        clipboardItem = { action: 'cut', name: itemName };
        closeDropdown();
    }

    function handlePaste(targetItem: Item) {
        if (clipboardItem && clipboardItem.name !== targetItem.name) {
            if (clipboardItem.action === 'cut') {
                onMoveItem(clipboardItem.name, targetItem.type === 'directory' ? targetItem.name : '');
                clipboardItem = null;
            } else {
                // TODO: 实现复制功能
                clipboardItem = null;
            }
        }
        closeDropdown();
    }

    function handleKeyDown(event: KeyboardEvent) {
        if (event.key === 'ArrowUp') {
            event.preventDefault();
            if (selectedIndex > 0) {
                selectedIndex--;
                scrollToSelectedItem();
            }
        } else if (event.key === 'ArrowDown') {
            event.preventDefault();
            if (selectedIndex < filteredItems.length - 1) {
                selectedIndex++;
                scrollToSelectedItem();
            }
        } else if (event.key === 'Enter') {
            if (selectedIndex >= 0 && selectedIndex < filteredItems.length) {
              handleShowFileInfo(
                filteredItems[selectedIndex]
              )
              
            }
        }
    }

    function scrollToSelectedItem() {
        const selectedElement = listElement?.querySelector(`[data-index="${selectedIndex}"]`);
        if (selectedElement) {
            selectedElement.scrollIntoView({ block: 'nearest' });
        }
    }

    function toggleDropdown(index: number, event: MouseEvent) {
        event.stopPropagation();
        if (dropdownOpen && dropdownIndex === index) {
            closeDropdown();
        } else {
            dropdownOpen = true;
            dropdownIndex = index;
            selectedIndex=index;
        }
    }

    function closeDropdown() {
        dropdownOpen = false;
    }

    function handleClickOutside(event: MouseEvent) {
        if (dropdownOpen) {
            const target = event.target as HTMLElement;
            const dropdown = document.querySelector('[role="menu"]');
            const button = document.querySelector(`[data-dropdown-trigger="${dropdownIndex}"]`);
            
            if (dropdown && button && 
                !dropdown.contains(target) && 
                !button.contains(target)) {
                closeDropdown();
            }
        }
    }

    onMount(() => {
        document.addEventListener('click', handleClickOutside);
        return ()=>{document.removeEventListener('click', handleClickOutside);}
    });

    // 在下拉菜单中添加新的选项
    let showFileInfo = $state(false);
    let currentFileInfo = $state<Item | null>(null);
    function handleShowFileInfo(item: Item) {
        currentFileInfo = item;
        showFileInfo = true;
        closeDropdown();
    }
    function getDropdownMenuItems(item: Item) {
        return [
            {
                label: item.type === 'directory' ? '打开' : '预览',
                onClick: () => { onOpenItem(item.name); closeDropdown(); },
                show: true
            },
            {
                label: '下载',
                onClick: () => { onDownloadFile(item.name); closeDropdown(); },
                show: item.type !== 'directory'
            },
            {
                label: '复制',
                onClick: () => { handleCopy(item.name); },
                show: true
            },
            {
                label: '剪切',
                onClick: () => { handleCut(item.name); },
                show: true
            },
            {
                label: '粘贴',
                onClick: () => { handlePaste(item); },
                show: item.type === 'directory' && clipboardItem !== null
            },
            {
                label: '删除',
                onClick: () => { onDeleteItem(item.name); closeDropdown(); },
                show: true
            },
            {
                label: '重命名',
                onClick: () => { onRenameItem(item.name); closeDropdown(); },
                show: true
            },
            {
                label: '简介',
                onClick: () => { handleShowFileInfo(item); },
                show: true
            }
        ];
    }

   

    function handleItemClick(index: number) {
        selectedIndex = index;
    }


  let sortField: 'name' | 'size' | 'lastModified' =$state('name');
  let sortDirection: 'asc' | 'desc' = $state('asc');

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
      minute: '2-digit'
    });
  }

  function toggleSort(field: 'name' | 'size' | 'lastModified') {
    if (sortField === field) {
      sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      sortField = field;
      sortDirection = 'asc';
    }
  }

  function getFileIcon(type: string): string {
    if (type === 'directory') {
      return `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-yellow-500" viewBox="0 0 20 20" fill="currentColor"><path d="M2 6a2 2 0 012-2h5l2 2h5a2 2 0 012 2v6a2 2 0 01-2 2H4a2 2 0 01-2-2V6z" /></svg>`;
    }

    const mimeType = type.toLowerCase();
    if (mimeType.includes('image')) {
      return `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-green-500" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M4 3a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2H4zm12 12H4l4-8 3 6 2-4 3 6z" clip-rule="evenodd" /></svg>`;
    } else if (mimeType.includes('video')) {
      return `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-blue-500" viewBox="0 0 20 20" fill="currentColor"><path d="M2 6a2 2 0 012-2h6a2 2 0 012 2v8a2 2 0 01-2 2H4a2 2 0 01-2-2V6zm12.553 1.106A1 1 0 0014 8v4a1 1 0 00.553.894l2 1A1 1 0 0018 13V7a1 1 0 00-1.447-.894l-2 1z" /></svg>`;
    } else if (mimeType.includes('audio')) {
      return `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-purple-500" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M9.383 3.076A1 1 0 0110 4v12a1 1 0 01-1.707.707L4.586 13H2a1 1 0 01-1-1V8a1 1 0 011-1h2.586l3.707-3.707a1 1 0 011.09-.217zM14.657 2.929a1 1 0 011.414 0A9.972 9.972 0 0119 10a9.972 9.972 0 01-2.929 7.071 1 1 0 01-1.414-1.414A7.971 7.971 0 0017 10c0-2.21-.894-4.208-2.343-5.657a1 1 0 010-1.414zm-2.829 2.828a1 1 0 011.415 0A5.983 5.983 0 0115 10a5.984 5.984 0 01-1.757 4.243 1 1 0 01-1.415-1.415A3.984 3.984 0 0013 10a3.983 3.983 0 00-1.172-2.828 1 1 0 010-1.415z" clip-rule="evenodd" /></svg>`;
    } else if (mimeType.includes('pdf')) {
      return `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-red-500" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4z" clip-rule="evenodd" /></svg>`;
    }

    return `<svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-500" viewBox="0 0 20 20" fill="currentColor"><path fill-rule="evenodd" d="M4 4a2 2 0 012-2h4.586A2 2 0 0112 2.586L15.414 6A2 2 0 0116 7.414V16a2 2 0 01-2 2H6a2 2 0 01-2-2V4z" clip-rule="evenodd" /></svg>`;
  }
let filteredItems=$derived(items
    .filter(file => file.name.toLowerCase().includes(searchQuery.toLowerCase()))
    .sort((a, b) => {
      const direction = sortDirection === 'asc' ? 1 : -1;
      if (sortField === 'name') {
        return direction * a.name.localeCompare(b.name);
      } else if (sortField === 'size') {
        if (a.type === 'directory' && b.type === 'directory') return 0;
        if (a.type === 'directory') return -1;
        if (b.type === 'directory') return 1;
        return direction * (a.size - b.size);
      } else {
        return direction * (new Date(a.lastModified).getTime() - new Date(b.lastModified).getTime());
      }
    }));
</script>

<div class="min-w-full divide-y divide-gray-200 max-h-[80vh] overflow-auto pb-48" bind:this={listElement} tabindex="0" onkeydown={handleKeyDown}>
  <div class="bg-gray-50 px-4 sm:px-6 py-2 sm:py-3 grid grid-cols-12 gap-2 sm:gap-4">
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div 
      class="col-span-8 sm:col-span-4 font-medium text-gray-500 text-sm sm:text-base flex items-center gap-1 cursor-pointer select-none"
      onclick={() => toggleSort('name')}
    >
      <span>文件名</span>
      <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" class:hidden={sortField !== 'name'}>
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d={sortDirection === 'asc' ? "M5 15l7-7 7 7" : "M19 9l-7 7-7-7"} />
      </svg>
    </div>
    <div 
      class="hidden sm:block sm:col-span-2 font-medium text-gray-500 text-sm sm:text-base flex items-center gap-1 cursor-pointer select-none"
      onclick={() => toggleSort('size')}
    >
      <span>大小</span>
      <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" class:hidden={sortField !== 'size'}>
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d={sortDirection === 'asc' ? "M5 15l7-7 7 7" : "M19 9l-7 7-7-7"} />
      </svg>
    </div>
    <div 
      class="hidden lg:block lg:col-span-3 font-medium text-gray-500 text-sm sm:text-base flex items-center gap-1 cursor-pointer select-none"
      onclick={() => toggleSort('lastModified')}
    >
      <span>修改时间</span>
      <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" class:hidden={sortField !== 'lastModified'}>
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d={sortDirection === 'asc' ? "M5 15l7-7 7 7" : "M19 9l-7 7-7-7"} />
      </svg>
    </div>
    <div class="sm:col-span-6 lg:col-span-3 col-span-4 text-center font-medium text-gray-500 text-sm sm:text-base">操作</div>
  </div>
  <div class="divide-y divide-gray-200">
    {#each filteredItems as item, i}
      <div
        data-index={i}
        onclick={() => handleItemClick(i)}
        ondblclick={(e) => {
          e.preventDefault();
          onOpenItem(item.name);
        }}
        draggable="true"
        ondragstart={(e) => handleDragStart(e, item.name)}
        ondragover={(e) => handleDragOver(e, item)}
        ondragleave={handleDragLeave}
        ondrop={(e) => handleDrop(e, item)}
        class="px-4 sm:px-6 py-3 sm:py-4 grid grid-cols-12 gap-2 sm:gap-4 hover:bg-gray-50 items-start {selectedIndex === i ? 'bg-blue-50 ring-2 ring-blue-200' : ''} {draggedItem === item.name ? 'opacity-50' : ''}"
        role="button"
        tabindex="-1"
      >
        <div class="col-span-8 sm:col-span-4 text-sm sm:text-base flex items-center gap-2">
          <div class="flex-shrink-0">
            {@html getFileIcon(item.type || '')}
          </div>
          <div class="flex flex-col justify-between truncate">

            <div class="truncate">
              <span>{item.name}</span>
            </div>
            <div class="lg:hidden text-xs">{formatDate(item.lastModified)}</div>
          </div>
        </div>
        <div class="hidden sm:block sm:col-span-2 text-sm sm:text-base">{formatFileSize(item.size, item.type === 'directory')}</div>
        <div class="hidden lg:block lg:col-span-3 sm:col-span-4 text-sm sm:text-base">{formatDate(item.lastModified)}</div>
        <div class="col-span-4 sm:col-span-6 lg:col-span-3">
            <div class="relative">
                <div class="hidden sm:flex gap-2 justify-center">
                            <!-- 桌面端下拉菜单 -->
                            <button
                                data-dropdown-trigger={i}
                                onclick={(e) => toggleDropdown(i, e)}
                                class="text-gray-500 hover:text-gray-700 text-sm sm:text-base px-4 py-1 border border-gray-500 rounded-full hover:bg-gray-50 transition-colors"
                            >
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 inline-block" viewBox="0 0 20 20" fill="currentColor">
                                    <path d="M6 10a2 2 0 11-4 0 2 2 0 014 0zM12 10a2 2 0 11-4 0 2 2 0 014 0zM16 12a2 2 0 100-4 2 2 0 000 4z" />
                                </svg>
                            </button>
                            {#if dropdownOpen && dropdownIndex === i}
                                <div 
                                    class="absolute right-0 mt-2 w-48 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5 z-50"
                                    role="menu"
                                >
                                    <div class="py-1" role="none">
                                        {#each getDropdownMenuItems(item) as menuItem}
                                            {#if menuItem.show}
                                                <button
                                                    onclick={menuItem.onClick}
                                                    class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                                                    role="menuitem"
                                                >
                                                    {menuItem.label}
                                                </button>
                                            {/if}
                                        {/each}
                                    </div>
                                </div>
                            {/if}
                </div>
                <!-- mobile operations -->
                <div class="sm:hidden flex justify-center">
                    <button
                        data-dropdown-trigger={i}
                        onclick={(e) => toggleDropdown(i, e)}
                        class="p-2 text-gray-500 hover:text-gray-700 transition-colors rounded-full hover:bg-gray-100"
                    >
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" viewBox="0 0 20 20" fill="currentColor">
                            <path d="M6 10a2 2 0 11-4 0 2 2 0 014 0zM12 10a2 2 0 11-4 0 2 2 0 014 0zM16 12a2 2 0 100-4 2 2 0 000 4z" />
                        </svg>
                    </button>
                    {#if dropdownOpen && dropdownIndex === i}
                        <div 
                            class="absolute right-0 mt-2 w-48 rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5 z-50"
                            role="menu"
                        >
                            <div class="py-1" role="none">
                                {#each getDropdownMenuItems(item) as menuItem}
                                    {#if menuItem.show}
                                        <button
                                            onclick={menuItem.onClick}
                                            class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                                            role="menuitem"
                                        >
                                            {menuItem.label}
                                        </button>
                                    {/if}
                                {/each}
                            </div>
                        </div>
                    {/if}
                </div>
            </div>
        </div>
      </div>
    {/each}
    {#if filteredItems.length === 0}
      <div class="px-4 sm:px-6 py-3 sm:py-4 text-center text-gray-500 text-sm sm:text-base">
        {searchQuery ? '没有找到匹配的文件' : '暂无文件'}
      </div>
    {/if}
  </div>
</div>

<FileInfoModal
    isOpen={showFileInfo}
    fileInfo={currentFileInfo}
    onClose={() => showFileInfo = false}
/>