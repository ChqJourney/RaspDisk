<script lang="ts">
  export let progress: number = 0;
  export let isUploading: boolean = false;
  export let isPaused: boolean = false;

  $: progressText = progress.toFixed(1);
  function onPause() {
    // @ts-ignore
    window.uploadController?.pause();
  }

  function onResume() {
    // @ts-ignore
    window.uploadController?.resume();
  }

  function onStop() {
    // @ts-ignore
    window.uploadController?.stop();
  }
</script>

{#if isUploading}
  <div class="mt-0 bg-transparent">
    <div class="flex items-center gap-4">
      <div class="flex-1 bg-gray-200 rounded-full h-2">
        <div
          class="bg-blue-500 h-2 rounded-full transition-all duration-300 ease-in-out"
          style="width: {progress}%"
        ></div>
      </div>
      <span class="text-sm text-gray-600 w-16">{progressText}%</span>
      <div class="flex gap-2">
        {#if !isPaused}
          <button
            class="w-8 h-8 cursor-pointer flex items-center justify-center rounded-full bg-yellow-500 text-white hover:bg-yellow-600 transition-colors"
            on:click={onPause}
            title="暂停"
          >
          <svg class="h-6 w-6" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg"  width="200" height="200"><path d="M352 768c-17.664 0-32-14.304-32-32L320 288c0-17.664 14.336-32 32-32s32 14.336 32 32l0 448C384 753.696 369.664 768 352 768z" fill="white" ></path><path d="M672 768c-17.696 0-32-14.304-32-32L640 288c0-17.664 14.304-32 32-32s32 14.336 32 32l0 448C704 753.696 689.696 768 672 768z" fill="white"></path></svg>
          </button>
        {:else}
          <button
            class="w-10 cursor-pointer h-10 flex items-center justify-center rounded-full bg-green-500 text-white hover:bg-green-600 transition-colors"
            on:click={onResume}
            title="继续"
          >
          <svg class="h44 w-5" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" width="200" height="200"><path d="M411.954935 647.899156c3.714602 0 7.42818-1.050935 11.0558-3.105734l200.001103-113.304556c7.470135-4.238534 11.361769-10.968819 11.361769-19.532869 0-8.500605-3.891633-15.226796-11.361769-19.445888L423.010735 379.205555c-3.671623-2.053776-7.341199-3.099594-11.0558-3.099594-3.802606 0-7.55814 1.113357-11.229762 3.230578-7.29822 4.218068-11.143805 10.949376-11.143805 19.338441l0 226.653113c0 8.389064 3.845585 15.116279 11.143805 19.336394C404.396795 646.780683 408.152329 647.899156 411.954935 647.899156M411.954935 670.248164c-7.691169 0-15.337313-2.073218-22.546505-6.29231-14.158465-8.215102-22.198581-22.24156-22.198581-38.626739L367.209848 398.674978c0-16.407692 8.040117-30.414707 22.198581-38.651298 7.209192-4.218068 14.856359-6.272867 22.546505-6.272867 7.517207 0 14.987343 1.990331 22.066575 6.007831l200.086038 113.305579c14.420431 8.172123 22.680559 22.329565 22.680559 38.891775 0 16.625656-8.260128 30.80561-22.680559 38.978756L434.022533 664.23931C426.9433 668.237367 419.472142 670.248164 411.954935 670.248164L411.954935 670.248164 411.954935 670.248164z"></path></svg>
          </button>
        {/if}
        <button 
          class="w-8 h-8 cursor-pointer flex items-center justify-center rounded-full bg-red-500 text-white hover:bg-red-600 transition-colors"
          on:click={onStop}
          title="停止"
        >
        <svg class="h-4 w-4" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg"  width="200" height="200"><path d="M896.215 874.19c0 17.51-14.508 32.018-32.018 32.018L159.811 906.208c-17.51 0-32.018-14.508-32.018-32.018L127.793 169.804c0-17.51 14.508-32.018 32.018-32.018l704.386 0c17.51 0 32.018 14.508 32.018 32.018L896.215 874.19z" fill="white"></path></svg>
        </button>
      </div>
    </div>
  </div>
{/if}