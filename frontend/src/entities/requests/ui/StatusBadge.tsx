import { Badge, type BadgeTone } from '@/shared/ui';
import { STATUS_LABEL, type RequestStatus } from '../model/requests';

const TONE: Record<RequestStatus, BadgeTone> = {
  Submitted: 'submitted',
  InProgress: 'progress',
  Ready: 'success',
  Issued: 'success',
  Rejected: 'danger',
  Cancelled: 'neutral',
};

export function StatusBadge({ status }: { status: RequestStatus }) {
  return <Badge tone={TONE[status]}>{STATUS_LABEL[status]}</Badge>;
}
