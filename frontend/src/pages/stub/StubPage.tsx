import { Construction } from 'lucide-react';
import { EmptyState } from '@/shared/ui';
import { ScreenHeader } from '@/widgets/app-shell';

export interface StubPageProps {
  title: string;
  /** Что здесь будет, одной фразой. */
  text: string;
}

/** Заглушка раздела, пока его экран не сделан. Навигация и шапка уже настоящие. */
export function StubPage({ title, text }: StubPageProps) {
  return (
    <>
      <ScreenHeader title={title} />
      <EmptyState icon={<Construction size={32} aria-hidden="true" />} title="Раздел пока в разработке" text={text} />
    </>
  );
}
