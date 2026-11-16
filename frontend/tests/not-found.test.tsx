import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { routes } from '@/app/router/routes';

describe('неизвестный адрес', () => {
  it('показывает страницу 404', async () => {
    const router = createMemoryRouter(routes, { initialEntries: ['/takoy-stranitsy-net'] });

    render(<RouterProvider router={router} />);

    expect(await screen.findByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/');
  });
});
