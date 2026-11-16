import type { HTMLAttributes } from 'react';
import { Link, type LinkProps } from 'react-router';
import { cx } from '@/shared/lib';
import styles from './Card.module.css';

export interface CardProps extends HTMLAttributes<HTMLElement> {
  as?: 'div' | 'section' | 'article' | 'li';
  raised?: boolean;
}

/** Отдельный объект: пара, заявка, объявление. Группы настроек и списки карточками не оформляем. */
export function Card({ as: Tag = 'div', raised, className, ...rest }: CardProps) {
  return <Tag className={cx(styles.card, raised && styles.raised, className)} {...rest} />;
}

export interface CardLinkProps extends LinkProps {
  raised?: boolean;
}

/** Карточка целиком ведёт в раздел, как «Следующая пара» на экране «Сегодня». */
export function CardLink({ raised, className, ...rest }: CardLinkProps) {
  return <Link className={cx(styles.card, styles.link, raised && styles.raised, className)} {...rest} />;
}
