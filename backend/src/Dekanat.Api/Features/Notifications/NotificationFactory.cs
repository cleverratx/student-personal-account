using Dekanat.Api.Features.Requests;
using Dekanat.Api.Features.Schedule;

namespace Dekanat.Api.Features.Notifications
{
    /// <summary>
    /// Создаёт уведомления по событиям домена. Не обращается к базе напрямую —
    /// возвращает готовые объекты, которые вызывающий код добавляет в <c>AppDbContext</c>
    /// в той же транзакции, что и само изменение.
    /// </summary>
    public static class NotificationFactory
    {
        /// <summary>
        /// Уведомление студенту о смене статуса его заявки.
        /// </summary>
        /// <param name="recipientUserId">Учётная запись студента-владельца заявки.</param>
        /// <param name="requestId">Id заявки для ссылки.</param>
        /// <param name="newStatus">Статус, в который перевела заявку.</param>
        /// <param name="typeName">Название типа заявки, например «Справка об обучении».</param>
        /// <param name="comment">Комментарий сотрудника (причина отказа, место выдачи и т.п.).</param>
        /// <param name="now">Текущее время UTC.</param>
        public static Notification RequestStatusChanged(
            Guid recipientUserId,
            Guid requestId,
            RequestStatus newStatus,
            string typeName,
            string? comment,
            DateTime now )
        {
            (string title, string content, bool isImportant) = newStatus switch
            {
                RequestStatus.InProgress => (
                    "Заявка взята в работу",
                    $"Ваша заявка «{typeName}» взята в обработку.",
                    false ),
                RequestStatus.Ready => (
                    "Справка готова",
                    string.IsNullOrWhiteSpace( comment )
                        ? $"Ваша заявка «{typeName}» готова к выдаче."
                        : $"Ваша заявка «{typeName}» готова. {comment.Trim()}",
                    true ),
                RequestStatus.Issued => (
                    "Справка выдана",
                    $"Ваша заявка «{typeName}» закрыта как выданная.",
                    false ),
                RequestStatus.Rejected => (
                    "Заявка отклонена",
                    string.IsNullOrWhiteSpace( comment )
                        ? $"Ваша заявка «{typeName}» отклонена."
                        : $"Ваша заявка «{typeName}» отклонена: {comment.Trim()}",
                    true ),
                _ => (
                    "Статус заявки изменился",
                    $"Статус заявки «{typeName}» изменён на «{RequestWorkflow.Title( newStatus )}».",
                    false ),
            };

            return new Notification
            {
                Id = Guid.NewGuid(),
                RecipientUserId = recipientUserId,
                Type = NotificationType.RequestStatus,
                Title = title,
                Content = content,
                IsImportant = isImportant,
                IsRead = false,
                CreatedAt = now,
                RequestId = requestId,
            };
        }

        /// <summary>
        /// Уведомления всем студентам группы о переносе или смене аудитории пары.
        /// Возвращает по одному уведомлению на каждого студента группы.
        /// </summary>
        /// <param name="recipientUserIds">Учётные записи студентов группы.</param>
        /// <param name="lessonId">Id пары для ссылки.</param>
        /// <param name="disciplineName">Название дисциплины.</param>
        /// <param name="changeType">Тип изменения: перенос или другая аудитория.</param>
        /// <param name="newStartsAt">Новое время начала пары (UTC). Null при смене только аудитории.</param>
        /// <param name="newRoom">Новая аудитория.</param>
        /// <param name="reason">Причина изменения.</param>
        /// <param name="now">Текущее время UTC.</param>
        public static IReadOnlyList<Notification> LessonChanged(
            IReadOnlyList<Guid> recipientUserIds,
            Guid lessonId,
            string disciplineName,
            LessonChangeType changeType,
            DateTime? newStartsAt,
            string newRoom,
            string? reason,
            DateTime now )
        {
            (string title, string content) = changeType switch
            {
                LessonChangeType.Reschedule when newStartsAt is not null => (
                    "Перенос пары",
                    string.IsNullOrWhiteSpace( reason )
                        ? $"Пара «{disciplineName}» перенесена. Новое время: {newStartsAt.Value:HH:mm} UTC, аудитория {newRoom}."
                        : $"Пара «{disciplineName}» перенесена ({reason.Trim()}). Новое время: {newStartsAt.Value:HH:mm} UTC, аудитория {newRoom}." ),
                LessonChangeType.RoomChange => (
                    "Смена аудитории",
                    string.IsNullOrWhiteSpace( reason )
                        ? $"Пара «{disciplineName}» пройдёт в аудитории {newRoom}."
                        : $"Пара «{disciplineName}» пройдёт в аудитории {newRoom} ({reason.Trim()})." ),
                LessonChangeType.Cancellation => (
                    "Отмена пары",
                    string.IsNullOrWhiteSpace( reason )
                        ? $"Пара «{disciplineName}» отменена."
                        : $"Пара «{disciplineName}» отменена: {reason.Trim()}." ),
                _ => (
                    "Изменение в расписании",
                    $"Изменение в расписании по дисциплине «{disciplineName}»." ),
            };

            return
            [
                .. recipientUserIds.Select( userId => new Notification
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = userId,
                    Type = NotificationType.LessonChange,
                    Title = title,
                    Content = content,
                    IsImportant = changeType == LessonChangeType.Reschedule || changeType == LessonChangeType.Cancellation,
                    IsRead = false,
                    CreatedAt = now,
                    LessonId = lessonId,
                } ),
            ];
        }
    }
}
