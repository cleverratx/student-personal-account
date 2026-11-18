import type { LessonType } from '../model/schedule';

export const LESSON_TYPE_LABEL: Record<LessonType, string> = {
  Lecture: 'Лекция',
  Practice: 'Практика',
  Lab: 'Лабораторная',
  Consultation: 'Консультация',
  Exam: 'Экзамен',
  Credit: 'Зачёт',
  GradedCredit: 'Дифзачёт',
};
