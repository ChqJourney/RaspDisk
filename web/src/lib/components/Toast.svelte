<script lang="ts">
  import { fade } from 'svelte/transition';
    import { toastStore } from './toastSstore';

  
</script>

<div class="fixed top-0 right-0 p-4 z-50 pointer-events-none">
  {#each $toastStore as toast}
    <div
      transition:fade
      class="mb-2 p-4 rounded-lg shadow-lg pointer-events-auto
        {toast.type === 'error' ? 'bg-red-500' :
         toast.type === 'success' ? 'bg-green-500' :
         'bg-blue-500'} text-white"
    >
      {toast.text}
    </div>
  {/each}
</div>