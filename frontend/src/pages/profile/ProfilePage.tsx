import { roleLine, useProfile, type Profile } from '@/entities/session';
import { LogoutButton } from '@/features/auth';
import { ThemeSwitch } from '@/features/theme';
import { Avatar, ConceptNote, ErrorState, ScreenSkeleton } from '@/shared/ui';
import { ScreenHeader } from '@/widgets/app-shell';
import styles from './ProfilePage.module.css';

interface Fact {
  label: string;
  value: string;
  numeric?: boolean;
}

function factsOf(profile: Profile): { title: string; facts: Fact[] } {
  const login: Fact = { label: 'Логин', value: profile.login };

  if (profile.student) {
    const { faculty, programCode, programName, course, group, gradeBookNumber } = profile.student;
    return {
      title: 'Учёба',
      facts: [
        { label: 'Факультет', value: faculty },
        { label: 'Направление', value: `${programCode} ${programName}` },
        { label: 'Курс и группа', value: `${course} курс, ${group}` },
        { label: 'Зачётная книжка', value: gradeBookNumber, numeric: true },
        login,
      ],
    };
  }

  if (profile.staff) {
    const { faculty, position, office } = profile.staff;
    return {
      title: 'Работа',
      facts: [
        { label: 'Подразделение', value: faculty },
        { label: 'Должность', value: position },
        ...(office ? [{ label: 'Кабинет', value: office }] : []),
        login,
      ],
    };
  }

  // Учётная запись без учебных или рабочих данных: показываем то, что есть.
  return { title: 'Учётная запись', facts: [login] };
}

function ProfileDetails({ profile }: { profile: Profile }) {
  const { title, facts } = factsOf(profile);
  const line = roleLine(profile);

  return (
    <>
      <section className={styles.card} aria-labelledby="profile-name">
        <Avatar className={styles.avatar} initials={profile.initials} size="large" />
        <h2 className={styles.name} id="profile-name">
          {profile.fullName}
        </h2>
        {line && <p className={styles.muted}>{line}</p>}
      </section>

      <section className={`${styles.section} ${styles.facts}`} aria-labelledby="profile-facts">
        <h2 className={styles.sectionTitle} id="profile-facts">
          {title}
        </h2>
        <dl className={styles.list}>
          {facts.map(({ label, value, numeric }) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd className={numeric ? styles.num : undefined}>{value}</dd>
            </div>
          ))}
        </dl>
      </section>
    </>
  );
}

export function ProfilePage() {
  const profile = useProfile();

  return (
    <>
      <ScreenHeader title="Профиль" />
      <div className={styles.page}>
        {profile.isPending ? (
          <div className={styles.card}>
            <ScreenSkeleton rows={3} label="Загружаем профиль" />
          </div>
        ) : profile.isError ? (
          <div className={styles.card}>
            <ErrorState noun="профиль" onRetry={() => profile.refetch()} retrying={profile.isFetching} />
          </div>
        ) : (
          <ProfileDetails profile={profile.data} />
        )}

        <section className={`${styles.section} ${styles.theme}`} aria-labelledby="profile-theme">
          <h2 className={styles.sectionTitle} id="profile-theme">
            Оформление
          </h2>
          <ThemeSwitch />
        </section>

        <ConceptNote className={styles.note} />

        <div className={styles.bar}>
          <LogoutButton />
        </div>
      </div>
    </>
  );
}
