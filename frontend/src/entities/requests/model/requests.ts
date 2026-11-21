import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, unwrap, type ApiError, type components } from '@/shared/api';

export type RequestStatus = components['schemas']['RequestStatus'];
export type RequestType = components['schemas']['RequestTypeDto'];

export type RequestItem = components['schemas']['RequestDto'];
export type RequestDetails = components['schemas']['RequestDetailsDto'];
export type RequestHistoryEntry = components['schemas']['RequestHistoryEntryDto'];
export type FieldSchema = Record<string, FieldDef>;

export interface FieldDef {
  type: 'string' | 'integer' | 'date' | 'uuid' | 'choice';
  required: boolean;
  title: string;
  maxLength?: number;
  min?: number;
  max?: number;
  options?: { value: string; title: string }[];
}

export const STATUS_LABEL: Record<RequestStatus, string> = {
  Submitted: 'Подана',
  InProgress: 'В работе',
  Ready: 'Готова',
  Issued: 'Выдана',
  Rejected: 'Отклонена',
  Cancelled: 'Отменена',
};

export const requestKeys = {
  types: () => ['requests', 'types'] as const,
  list: (status?: RequestStatus) => ['requests', 'list', status ?? null] as const,
  detail: (id: string) => ['requests', 'detail', id] as const,
};

export function useRequestTypes() {
  return useQuery<RequestType[], ApiError>({
    queryKey: requestKeys.types(),
    queryFn: () => unwrap(api.GET('/api/request-types')).then((r) => r.items),
  });
}

export function useRequests(status?: RequestStatus) {
  return useQuery<RequestItem[], ApiError>({
    queryKey: requestKeys.list(status),
    queryFn: () =>
      unwrap(api.GET('/api/requests', { params: { query: { status } } })).then((r) => r.items),
  });
}

export function useRequestDetail(id: string) {
  return useQuery<RequestDetails, ApiError>({
    queryKey: requestKeys.detail(id),
    queryFn: () => unwrap(api.GET('/api/requests/{id}', { params: { path: { id } } })),
    enabled: !!id,
  });
}

export function useCreateRequest() {
  const queryClient = useQueryClient();
  return useMutation<RequestDetails, ApiError, { requestTypeId: string; fields: Record<string, unknown> }>({
    mutationFn: (body) =>
      unwrap(api.POST('/api/requests', { body: { requestTypeId: body.requestTypeId, fields: body.fields } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['requests', 'list'] });
    },
  });
}

export function useCancelRequest(id: string) {
  const queryClient = useQueryClient();
  return useMutation<RequestDetails, ApiError>({
    mutationFn: () => unwrap(api.POST('/api/requests/{id}/cancel', { params: { path: { id } } })),
    onSuccess: (data) => {
      queryClient.setQueryData(requestKeys.detail(id), data);
      void queryClient.invalidateQueries({ queryKey: ['requests', 'list'] });
    },
  });
}
