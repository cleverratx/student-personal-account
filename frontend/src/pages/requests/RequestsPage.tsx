import { FileText, Plus } from 'lucide-react';
import { Link } from 'react-router';

import { StatusBadge, useRequests, type RequestItem, type RequestStatus } from '@/entities/requests';
import { ButtonLink, EmptyState, ErrorState, ScreenSkeleton } from '@/shared/ui';
import { ScreenHeader } from '@/widgets/app-shell';
import styles from './RequestsPage.module.css';

const ACTIVE: RequestStatus[] = ['Submitted', 'InProgress', 'Ready'];

function isActive(status: RequestStatus): boolean {
  return ACTIVE.includes(status);
}

function formatDate(iso: string | null): string | null {
  if (!iso) return null;
  // estimatedReadyDate приходит как "YYYY-MM-DD"
  const [year, month, day] = iso.split('-');
  return `${day}.${month}.${year}`;
}

function RequestCard({ item }: { item: RequestItem }) {
  const ready = formatDate(item.estimatedReadyDate);
  return (
    <Link to={`/requests/${item.id}`} className={styles.card}>
      <div className={styles.cardTop}>
        <span className={styles.cardName}>{item.type.name}</span>
        <StatusBadge status={item.status} />
      </div>
      {item.status === 'Ready' && item.issuePlace && (
        <p className={styles.cardHint}>Забрать: {item.issuePlace}</p>
      )}
      {item.status === 'Rejected' && (
        <p className={styles.cardHintDanger}>Отклонена — откройте для подробностей</p>
      )}
      {ready && isActive(item.status) && item.status !== 'Ready' && (
        <p className={styles.cardHint}>Примерно готова {ready}</p>
      )}
    </Link>
  );
}

export function RequestsPage() {
  const { data, isPending, isError, error, refetch } = useRequests();

  const active = data?.filter((r) => isActive(r.status)) ?? [];
  const done = data?.filter((r) => !isActive(r.status)) ?? [];

  return (
    <>
      <ScreenHeader title="Заявки" />

      {isPending && <ScreenSkeleton rows={3} label="Загружаем заявки" />}

      {isError && (
        <ErrorState noun="заявки" message={error.message} onRetry={() => void refetch()} />
      )}

      {data && data.length === 0 && (
        <EmptyState
          icon={<FileText size={32} aria-hidden="true" />}
          title="Заявок пока нет"
          text="Здесь появятся справки и заявления, которые вы подали в деканат."
          action={
            <ButtonLink to="/requests/new" icon={<Plus size={18} aria-hidden="true" />}>
              Подать заявку
            </ButtonLink>
          }
        />
      )}

      {data && data.length > 0 && (
        <div className={styles.body}>
          {active.length > 0 && (
            <section aria-labelledby="active-heading">
              <h2 className={styles.sectionTitle} id="active-heading">
                Активные
              </h2>
              <ul className={styles.list}>
                {active.map((r) => (
                  <li key={r.id}>
                    <RequestCard item={r} />
                  </li>
                ))}
              </ul>
            </section>
          )}

          {done.length > 0 && (
            <section aria-labelledby="done-heading">
              <h2 className={styles.sectionTitle} id="done-heading">
                Завершённые
              </h2>
              <ul className={styles.list}>
                {done.map((r) => (
                  <li key={r.id}>
                    <RequestCard item={r} />
                  </li>
                ))}
              </ul>
            </section>
          )}

          <div className={styles.newBtn}>
            <ButtonLink to="/requests/new" icon={<Plus size={18} aria-hidden="true" />} block>
              Новая заявка
            </ButtonLink>
          </div>
        </div>
      )}
    </>
  );
}
