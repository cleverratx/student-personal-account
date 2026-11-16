/**
 * Адрес, куда вернуть после входа. Берём только путь внутри сайта: «//evil.example» и
 * «https://…» отбрасываем, иначе ссылкой на вход можно увести человека на чужой сайт.
 */
export function safeReturnTo(value: string | null | undefined): string | undefined {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) return undefined;
  if (value === '/login' || value.startsWith('/login?')) return undefined;
  return value;
}
