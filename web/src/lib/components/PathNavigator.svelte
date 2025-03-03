<script lang="ts">
  export let currentPath: string;
  export let onNavigate: (path: string) => void;

  let showDropdown = false;
  let dropdownRef: HTMLDivElement;

  // 计算从根目录到当前目录的所有路径
  $: pathSegments = currentPath
    ? currentPath.split('/').reduce((acc, segment, index, arr) => {
        if (segment) {
          acc.push({
            name: segment,
            path: arr.slice(0, index + 1).join('/')
          });
        }
        return acc;
      }, [] as Array<{name: string; path: string}>)
    : [];

  // 点击外部关闭下拉菜单
  function handleClickOutside(event: MouseEvent) {
    if (dropdownRef && !dropdownRef.contains(event.target as Node)) {
      showDropdown = false;
    }
  }

  // 组件挂载时添加点击事件监听
  import { onMount, onDestroy } from 'svelte';

  onMount(() => {
    document.addEventListener('click', handleClickOutside);
    return ()=>document.removeEventListener('click', handleClickOutside);
  });

</script>

<div class="relative rounded-md bg-slate-100 max-w-36 sm:max-w-48 mx-auto" bind:this={dropdownRef}>
  <button
    aria-label="pathNavigator"
    class="text-gray-500 w-full px-3 py-1 rounded-md text-center text-md hover:bg-gray-100 transition-colors flex items-center justify-center gap-2"
    on:click={() => (showDropdown = !showDropdown)}
  >
    <span class="truncate">{currentPath === "" ? "root" : currentPath}</span>
    <svg
      xmlns="http://www.w3.org/2000/svg"
      class="h-5 w-5 flex-shrink-0 transform transition-transform {showDropdown ? 'rotate-180' : ''}"
      viewBox="0 0 20 20"
      fill="currentColor"
    >
      <path
        fill-rule="evenodd"
        d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z"
        clip-rule="evenodd"
      />
    </svg>
  </button>

  {#if showDropdown}
    <div
      class="absolute z-10 mt-1 w-full bg-white rounded-md shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none"
    >
      <div class="py-1">
        <button
          class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
          on:click={() => {
            onNavigate("");
            showDropdown = false;
          }}
        >
          root
        </button>
        {#each pathSegments as segment}
          <button
            class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 truncate"
            on:click={() => {
              onNavigate(segment.path);
              showDropdown = false;
            }}
          >
            {segment.name}
          </button>
        {/each}
      </div>
    </div>
  {/if}
</div>