import { writable } from "svelte/store";

export const toastStore = writable<ToastMessage[]>([]);


interface ToastMessage {
    type: 'success' | 'error' | 'info';
    text: string;
  }

   // 显示Toast消息
   export function showToast(message: string, type: ToastMessage['type'] = 'info') {
    toastStore.update(messages => [
      ...messages,
      { type, text: message }
    ]);

    // 3秒后自动移除
    setTimeout(() => {
      toastStore.update(messages => messages.slice(1));
    }, 3000);
  }