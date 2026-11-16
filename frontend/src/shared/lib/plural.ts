const rules = new Intl.PluralRules('ru-RU');

export interface PluralForms {
  one: string;
  few: string;
  many: string;
}

/** «1 минута», «3 минуты», «5 минут». */
export function plural(count: number, forms: PluralForms): string {
  const rule = rules.select(count);
  const form = rule === 'one' ? forms.one : rule === 'few' ? forms.few : forms.many;
  return `${count}\u00a0${form}`;
}
