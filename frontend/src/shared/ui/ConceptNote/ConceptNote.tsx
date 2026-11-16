import { Info } from 'lucide-react';
import { cx } from '@/shared/lib';
import styles from './ConceptNote.module.css';

/** Пометка, которую конкурс требует на входе и в профиле: это концепт, а не сервис вуза. */
export function ConceptNote({ className }: { className?: string }) {
  return (
    <p className={cx(styles.note, className)}>
      <Info size={16} aria-hidden="true" />
      <span>Это концепт для конкурса, а не официальный сервис вуза. Все люди, оценки и заявки здесь вымышленные.</span>
    </p>
  );
}
