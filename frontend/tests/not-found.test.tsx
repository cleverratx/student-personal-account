import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { routes } from '@/app/router/routes';

describe('неизвестный адрес', () => {
  it('показывает страницу 404 без меню кабинета', async () => {
    const router = createMemoryRouter(routes, { initialEntries: ['/takoy-stranitsy-net'] });

    render(<RouterProvider router={router} />);

    expect(await screen.findByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/');
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('ловит и опечатки внутри кабинета сотрудника', async () => {
    const router = createMemoryRouter(routes, { initialEntries: ['/staff/zayavki'] });

    render(<RouterProvider router={router} />);

    expect(await screen.findByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
  });
});
