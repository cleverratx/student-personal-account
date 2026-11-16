import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { MemoryRouter } from 'react-router';
import { Button, ErrorState, IconButton, SegmentedControl, Select, TextField, ToastProvider, useToast } from '@/shared/ui';

describe('Button', () => {
  it('во время загрузки не нажимается и сообщает, что занята', async () => {
    const onClick = vi.fn();
    render(
      <Button loading onClick={onClick}>
        Войти
      </Button>,
    );

    const button = screen.getByRole('button', { name: 'Войти' });
    await userEvent.click(button);

    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
    expect(onClick).not.toHaveBeenCalled();
  });
});

describe('IconButton', () => {
  it('подписан для скринридера, счётчик не дублирует подпись', () => {
    render(<IconButton label="Уведомления, 3 непрочитанных" icon={<svg />} counter={3} />);

    expect(screen.getByRole('button', { name: 'Уведомления, 3 непрочитанных' })).toBeInTheDocument();
  });
});

describe('TextField', () => {
  it('связывает ошибку с полем', () => {
    render(<TextField label="Логин" error="Введите логин" hint="Как в письме из деканата" />);

    const input = screen.getByLabelText('Логин');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('Как в письме из деканата Введите логин');
  });

  it('показывает и прячет пароль', async () => {
    render(<TextField label="Пароль" type="password" revealable />);

    const input = screen.getByLabelText('Пароль');
    const toggle = screen.getByRole('button', { name: 'Показать пароль' });
    expect(input).toHaveAttribute('type', 'password');

    await userEvent.click(toggle);
    expect(input).toHaveAttribute('type', 'text');
    expect(toggle).toHaveAttribute('aria-pressed', 'true');

    await userEvent.click(toggle);
    expect(input).toHaveAttribute('type', 'password');
  });
});

describe('Select', () => {
  it('подписан и выбирает значение', async () => {
    render(
      <Select label="Тип справки" defaultValue="study">
        <option value="study">Справка об обучении</option>
        <option value="stipend">Справка о стипендии</option>
      </Select>,
    );

    await userEvent.selectOptions(screen.getByLabelText('Тип справки'), 'stipend');
    expect(screen.getByLabelText('Тип справки')).toHaveValue('stipend');
  });
});

describe('ErrorState', () => {
  it('говорит, что не загрузилось, и даёт повторить', async () => {
    const onRetry = vi.fn();
    render(<ErrorState noun="расписание" onRetry={onRetry} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Не удалось загрузить расписание');
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});

describe('SegmentedControl', () => {
  function Filter() {
    const [value, setValue] = useState<'active' | 'done'>('active');
    return (
      <SegmentedControl
        label="Какие заявки показать"
        value={value}
        onChange={setValue}
        options={[
          { value: 'active', label: 'Активные' },
          { value: 'done', label: 'Завершённые' },
        ]}
      />
    );
  }

  it('переключается кликом и стрелками', async () => {
    render(<Filter />);
    const active = screen.getByRole('radio', { name: 'Активные' });
    const done = screen.getByRole('radio', { name: 'Завершённые' });

    await userEvent.click(done);
    expect(done).toHaveAttribute('aria-checked', 'true');

    await userEvent.keyboard('{ArrowRight}');
    expect(active).toHaveAttribute('aria-checked', 'true');
    expect(active).toHaveFocus();
  });
});

describe('Toast', () => {
  function SendButton({ onUndo }: { onUndo: () => void }) {
    const toast = useToast();
    return <button onClick={() => toast('Заявка отправлена', { undo: onUndo })}>Отправить</button>;
  }

  it('показывает сообщение с отменой и прячет его через несколько секунд', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const onUndo = vi.fn();
    render(
      <MemoryRouter>
        <ToastProvider>
          <SendButton onUndo={onUndo} />
        </ToastProvider>
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Отправить' }));
    expect(screen.getByRole('status')).toHaveTextContent('Заявка отправлена');

    await userEvent.click(screen.getByRole('button', { name: 'Отменить' }));
    expect(onUndo).toHaveBeenCalledOnce();
    expect(screen.getByRole('status')).toBeEmptyDOMElement();

    await userEvent.click(screen.getByRole('button', { name: 'Отправить' }));
    act(() => vi.advanceTimersByTime(6500));
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
    vi.useRealTimers();
  });
});
