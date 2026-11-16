import { Bell, FileText, Moon, Send, Sun, SunMoon } from 'lucide-react';
import { useState } from 'react';
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorState,
  Header,
  IconButton,
  ScreenSkeleton,
  SegmentedControl,
  Select,
  Sheet,
  TextField,
  useToast,
} from '@/shared/ui';
import styles from './DevUiPage.module.css';

type Theme = 'system' | 'light' | 'dark';

function applyTheme(theme: Theme) {
  if (theme === 'system') delete document.documentElement.dataset.theme;
  else document.documentElement.dataset.theme = theme;
}

/** Витрина UI-кита для разработки. В сборку для пользователей не попадает. */
export function DevUiPage() {
  const toast = useToast();
  const [theme, setTheme] = useState<Theme>('system');
  const [filter, setFilter] = useState<'active' | 'done'>('active');
  const [sheetOpen, setSheetOpen] = useState(false);
  const [loading, setLoading] = useState(false);

  const fakeSubmit = () => {
    setLoading(true);
    setTimeout(() => {
      setLoading(false);
      toast('Заявка отправлена', { undo: () => toast('Отправка отменена') });
    }, 1200);
  };

  return (
    <main className={styles.page}>
      <Header
        title="UI-кит"
        meta="страница только для разработки"
        actions={<IconButton label="Уведомления, 3 непрочитанных" icon={<Bell size={22} aria-hidden="true" />} counter={3} />}
      />

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Тема</h2>
        <SegmentedControl<Theme>
          full
          label="Тема оформления"
          value={theme}
          onChange={(value) => {
            setTheme(value);
            applyTheme(value);
          }}
          options={[
            { value: 'system', label: 'Системная', icon: <SunMoon size={18} aria-hidden="true" /> },
            { value: 'light', label: 'Светлая', icon: <Sun size={18} aria-hidden="true" /> },
            { value: 'dark', label: 'Тёмная', icon: <Moon size={18} aria-hidden="true" /> },
          ]}
        />
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Кнопки</h2>
        <div className={styles.row}>
          <Button icon={<Send size={18} aria-hidden="true" />} loading={loading} onClick={fakeSubmit}>
            Отправить заявку
          </Button>
          <Button variant="secondary">Отмена</Button>
          <Button variant="danger">Отозвать заявку</Button>
          <Button variant="danger-text">Выйти</Button>
          <Button disabled>Недоступно</Button>
          <Button size="small" variant="secondary">
            Маленькая
          </Button>
        </div>
        <Button block>Войти</Button>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Поля</h2>
        <div className={styles.stack}>
          <TextField label="Логин" autoComplete="username" placeholder="a.ivanova" />
          <TextField label="Пароль" type="password" revealable autoComplete="current-password" />
          <TextField label="Куда предоставить" error="Укажите организацию, куда нужна справка" />
          <TextField label="Комментарий" optional hint="Увидит только сотрудник деканата" />
          <Select label="Тип справки" defaultValue="study">
            <option value="study">Справка об обучении</option>
            <option value="stipend">Справка о размере стипендии</option>
            <option value="military">Справка для военкомата</option>
          </Select>
        </div>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Статусы и переключатель</h2>
        <div className={styles.row}>
          <Badge tone="submitted">Отправлена</Badge>
          <Badge tone="progress">В работе</Badge>
          <Badge tone="success">Готова</Badge>
          <Badge tone="danger">Отклонена</Badge>
          <Badge>Черновик</Badge>
        </div>
        <SegmentedControl
          full
          label="Какие заявки показать"
          value={filter}
          onChange={setFilter}
          options={[
            { value: 'active', label: 'Активные', count: 2 },
            { value: 'done', label: 'Завершённые', count: 7 },
          ]}
        />
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Карточка, лист и тост</h2>
        <Card raised>
          <p className={styles.cardTitle}>Справка об обучении для предоставления по месту работы родителей</p>
          <p className={styles.cardMeta}>Подана 12 ноября, ответ до 19 ноября</p>
        </Card>
        <div className={styles.row}>
          <Button variant="secondary" icon={<FileText size={18} aria-hidden="true" />} onClick={() => setSheetOpen(true)}>
            Открыть лист
          </Button>
          <Button variant="secondary" onClick={() => toast('Все уведомления прочитаны')}>
            Показать тост
          </Button>
        </div>
        <Sheet open={sheetOpen} onClose={() => setSheetOpen(false)} title="Отозвать заявку?" subtitle="Справка об обучении">
          <p>Деканат перестанет её обрабатывать. Подать заново можно в любой момент.</p>
          <Button variant="danger" block onClick={() => setSheetOpen(false)}>
            Отозвать
          </Button>
          <Button variant="secondary" block onClick={() => setSheetOpen(false)}>
            Оставить
          </Button>
        </Sheet>
      </section>

      <section className={styles.section}>
        <h2 className={styles.sectionTitle}>Состояния экрана</h2>
        <ScreenSkeleton />
        <ErrorState noun="расписание" onRetry={() => toast('Пробуем ещё раз')} />
        <EmptyState
          title="Заявок пока нет"
          text="Здесь появятся справки и заявления, которые вы подадите в деканат."
          action={<Button>Подать заявку</Button>}
        />
      </section>
    </main>
  );
}
