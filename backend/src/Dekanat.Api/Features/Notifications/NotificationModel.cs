using Dekanat.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Notifications
{
    /// <summary>
    /// Тип уведомления. Определяет заголовок, текст и какие поля links заполнены.
    /// </summary>
    public enum NotificationType
    {
        /// <summary>Статус заявки изменился (сотрудник сменил статус).</summary>
        RequestStatus,

        /// <summary>Пара перенесена или изменилась аудитория.</summary>
        LessonChange,
    }

    /// <summary>
    /// Личное уведомление студента. Создаётся в той же транзакции, что и событие.
    /// Время в UTC.
    /// </summary>
    public sealed class Notification
    {
        public Guid Id { get; set; }

        /// <summary>Кому: учётная запись студента.</summary>
        public Guid RecipientUserId { get; set; }

        public NotificationType Type { get; set; }

        public required string Title { get; set; }

        public required string Content { get; set; }

        public bool IsImportant { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }

        /// <summary>Id заявки, на которую ссылается уведомление. Заполнено при Type=RequestStatus.</summary>
        public Guid? RequestId { get; set; }

        /// <summary>Id пары, на которую ссылается уведомление. Заполнено при Type=LessonChange.</summary>
        public Guid? LessonId { get; set; }
    }

    public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure( EntityTypeBuilder<Notification> builder )
        {
            builder.ToTable( "notifications", table =>
                table.HasCheckConstraint(
                    "ck_notifications_type",
                    "type IN ('RequestStatus', 'LessonChange')" ) );

            builder.Property( n => n.Type ).HasConversion<string>().HasMaxLength( 32 );
            builder.Property( n => n.Title ).HasMaxLength( 200 );
            builder.Property( n => n.Content ).HasMaxLength( 1000 );

            // Лента уведомлений студента, новые сверху.
            builder.HasIndex( n => new { n.RecipientUserId, n.CreatedAt } ).IsDescending( false, true );

            // Счётчик непрочитанных.
            builder.HasIndex( n => new { n.RecipientUserId, n.IsRead } );

            builder.HasOne<AppUser>().WithMany()
                .HasForeignKey( n => n.RecipientUserId )
                .HasConstraintName( "fk_notifications_users_recipient_user_id" )
                .OnDelete( DeleteBehavior.Cascade );
        }
    }
}
