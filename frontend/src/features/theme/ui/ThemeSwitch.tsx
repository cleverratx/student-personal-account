import { SegmentedControl } from '@/shared/ui';
import { useTheme, type Theme } from '../model/theme';

export function ThemeSwitch({ label = 'Оформление' }: { label?: string }) {
  const [theme, setTheme] = useTheme();

  return (
    <SegmentedControl<Theme>
      full
      label={label}
      value={theme}
      onChange={setTheme}
      options={[
        { value: 'system', label: 'Системная' },
        { value: 'light', label: 'Светлая' },
        { value: 'dark', label: 'Тёмная' },
      ]}
    />
  );
}
