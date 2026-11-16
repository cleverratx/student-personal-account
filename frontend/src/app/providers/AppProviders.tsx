import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { useSessionReset } from '@/entities/session';
import { ToastProvider } from '@/shared/ui';

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: 1,
        refetchOnWindowFocus: false,
      },
    },
  });
}

function SessionSync() {
  useSessionReset();
  return null;
}

/** queryClient передают тесты, чтобы выключить повторы запросов. */
export function AppProviders({ children, queryClient }: { children: ReactNode; queryClient?: QueryClient }) {
  const [client] = useState(() => queryClient ?? createQueryClient());

  return (
    <QueryClientProvider client={client}>
      <SessionSync />
      <ToastProvider>{children}</ToastProvider>
    </QueryClientProvider>
  );
}
