import { CircleCheck, CircleX, Clock, Package } from 'lucide-react';
import { useParams } from 'react-router';
import { StatusBadge, useCancelRequest, useRequestDetail, STATUS_LABEL, type RequestHistoryEntry, type RequestStatus } from '@/entities/requests';
import { Button, Card, ErrorState, ScreenSkeleton } from '@/shared/ui';
import { ScreenHeader } from '@/widgets/app-shell';
import styles from './RequestDetailPage.module.css';

const TIMELINE_ORDER: RequestStatus[] = ['Submitted', 'InProgress', 'Ready', 'Issued'];

function timelineIcon(status: RequestStatus, done: boolean) {
  if (status === 'Rejected') return <CircleX size={20} aria-hidden="true" />;
  if (status === 'Cancelled') return <CircleX size={20} aria-hidden="true" />;
  if (done) return <CircleCheck size={20} aria-hidden="true" />;
  if (status === 'InProgress') return <Package size={20} aria-hidden="true" />;
  return <Clock size={20} aria-hidden="true" />;
}

function formatDateTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString('ru-RU', { day: '2-digit', month: 'long', hour: '2-digit', minute: '2-digit' });
}

function formatDate(iso: string): string {
  const [y, m, day] = iso.split('-');
  return `${day}.${m}.${y}`;
}

function Timeline({ history, currentStatus }: { history: RequestHistoryEntry[]; currentStatus: RequestStatus }) {
  const isFinal = ['Issued', 'Rejected', 'Cancelled'].includes(currentStatus);
  const historyMap = new Map(history.map((e) => [e.status, e]));

  const steps: { status: RequestStatus; label: string; entry?: RequestHistoryEntry; pending: boolean }[] =
    currentStatus === 'Rejected' || currentStatus === 'Cancelled'
      ? // For terminal negative: show actual history entries
        history.map((e) => ({ status: e.status, label: STATUS_LABEL[e.status], entry: e, pending: false }))
      : TIMELINE_ORDER.map((status) => {
          const entry = historyMap.get(status);
          const idx = TIMELINE_ORDER.indexOf(status);
          const curIdx = TIMELINE_ORDER.indexOf(currentStatus as RequestStatus);
          return {
            status,
            label: STATUS_LABEL[status],
            entry,
            pending: idx > curIdx,
          };
        });

  return (
    <ul className={styles.timeline} aria-label="История статусов">
      {steps.map(({ status, label, entry, pending }, i) => {
        const done = !!entry && !pending;
        const isLast = i === steps.length - 1;
        const isCurrent = status === currentStatus && !pending;
        return (
          <li key={`${status}-${i}`} className={styles.timelineItem} data-done={done} data-current={isCurrent}>
            <div className={styles.timelineGraphic}>
              <span className={styles.timelineDot} data-done={done} data-danger={['Rejected','Cancelled'].includes(status)}>
                {timelineIcon(status, done)}
              </span>
              {!isLast && <span className={styles.timelineLine} data-done={done} />}
            </div>
            <div className={styles.timelineContent}>
              <span className={styles.timelineLabel} data-done={done} data-pending={pending}>{label}</span>
              {entry && <span className={styles.timelineTime}>{formatDateTime(entry.changedAt)}</span>}
              {entry?.comment && (
                <span className={styles.timelineComment} data-danger={status === 'Rejected'}>
                  {status === 'Rejected' ? `Причина: ${entry.comment}` : entry.comment}
                </span>
              )}
              {pending && <span className={styles.timelinePending}>Ожидается</span>}
            </div>
          </li>
        );
      })}
    </ul>
  );
}

export function RequestDetailPage() {
  const { id = '' } = useParams<{ id: string }>();
  const { data, isPending, isError, error, refetch } = useRequestDetail(id);
  const cancel = useCancelRequest(id);

  if (isPending) return (
    <>
      <ScreenHeader title="Заявка" backTo="/requests" />
      <ScreenSkeleton rows={4} label="Загружаем заявку" />
    </>
  );

  if (isError) return (
    <>
      <ScreenHeader title="Заявка" backTo="/requests" />
      <ErrorState noun="заявку" message={error.message} onRetry={() => void refetch()} />
    </>
  );

  const isReady = data.status === 'Ready';
  const isRejected = data.status === 'Rejected';

  return (
    <>
      <ScreenHeader title={data.type.name} backTo="/requests" />

      <div className={styles.body}>
        {/* Статус и дата */}
        <Card className={styles.statusCard}>
          <div className={styles.statusRow}>
            <StatusBadge status={data.status} />
            {data.estimatedReadyDate && !['Issued','Rejected','Cancelled'].includes(data.status) && (
              <span className={styles.statusHint}>
                Примерно готова {formatDate(data.estimatedReadyDate)}
              </span>
            )}
          </div>

          {isReady && data.issuePlace && (
            <div className={styles.pickupBox} role="note" aria-label="Место получения">
              <p className={styles.pickupLabel}>Где забрать</p>
              <p className={styles.pickupValue}>{data.issuePlace}</p>
              {data.issueDeadline && (
                <p className={styles.pickupDeadline}>до {formatDateTime(data.issueDeadline)}</p>
              )}
            </div>
          )}

          {isRejected && (() => {
            const rejEntry = data.history.find((e) => e.status === 'Rejected');
            return rejEntry?.comment ? (
              <div className={styles.rejectBox} role="note" aria-label="Причина отказа">
                <p className={styles.rejectLabel}>Причина отказа</p>
                <p className={styles.rejectValue}>{rejEntry.comment}</p>
              </div>
            ) : null;
          })()}
        </Card>

        {/* Таймлайн */}
        <Card className={styles.timelineCard}>
          <h2 className={styles.sectionTitle}>Статусы</h2>
          <Timeline history={data.history} currentStatus={data.status} />
        </Card>

        {/* Отмена */}
        {data.canCancel && (
          <Button
            variant="danger-text"
            block
            loading={cancel.isPending}
            onClick={() => cancel.mutate()}
          >
            Отменить заявку
          </Button>
        )}
        {cancel.isError && (
          <p className={styles.cancelError} role="alert">{cancel.error.message}</p>
        )}
      </div>
    </>
  );
}
