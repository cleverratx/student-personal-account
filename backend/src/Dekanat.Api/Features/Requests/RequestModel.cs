using Dekanat.Api.Features.Auth;
using Dekanat.Api.Features.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dekanat.Api.Features.Requests
{
    /// <summary>
    /// Статус заявки. В базе и в API хранится английский код, русские названия показывает интерфейс.
    /// Какие переходы между статусами допустимы, решает только <see cref="RequestWorkflow"/>.
    /// </summary>
    public enum RequestStatus
    {
        Submitted,
        InProgress,
        Ready,
        Issued,
        Rejected,
        Cancelled,
    }

    /// <summary>
    /// Тип заявки: справка или заявление со своим набором полей. Справочник, см. <see cref="RequestTypes"/>.
    /// </summary>
    public sealed class RequestType
    {
        public Guid Id { get; set; }

        public required string Name { get; set; }

        public required string Description { get; set; }

        /// <summary>Срок изготовления в рабочих днях, по нему считается примерная дата готовности.</summary>
        public int ProcessingDays { get; set; }

        /// <summary>Описание полей заявки этого типа, JSON.</summary>
        public required string FieldSchema { get; set; }
    }

    /// <summary>
    /// Заявка студента. Статус меняется только через <see cref="RequestWorkflow"/>, который заодно пишет
    /// <see cref="History"/>. Время в UTC.
    /// </summary>
    public sealed class Request
    {
        public Guid Id { get; set; }

        public Guid StudentProfileId { get; set; }

        public StudentProfile StudentProfile { get; set; } = null!;

        public Guid RequestTypeId { get; set; }

        public RequestType Type { get; set; } = null!;

        /// <summary>Значения полей заявки, JSON-объект по схеме типа.</summary>
        public required string Fields { get; set; }

        public DateTime CreatedAt { get; set; }

        public RequestStatus CurrentStatus { get; set; }

        /// <summary>Примерная дата готовности по времени вуза.</summary>
        public DateOnly? EstimatedReadyDate { get; set; }

        /// <summary>Где забрать. Заполняется при переходе в «Готова».</summary>
        public string? IssuePlace { get; set; }

        /// <summary>До какого времени забрать, UTC. Заполняется при переходе в «Готова».</summary>
        public DateTime? IssueDeadline { get; set; }

        /// <summary>
        /// Номер версии для оптимистичной блокировки: растёт при каждой смене статуса. Если студент отменяет заявку,
        /// а сотрудник в ту же секунду берёт её в работу, второе сохранение падает с DbUpdateConcurrencyException.
        /// </summary>
        public int Version { get; set; }

        public List<RequestStatusHistory> History { get; set; } = [];
    }

    /// <summary>
    /// Одна запись истории: в какой статус и когда перевели заявку, кто это сделал и с каким комментарием.
    /// У отклонённой заявки в комментарии причина отказа. Время в UTC.
    /// </summary>
    public sealed class RequestStatusHistory
    {
        public Guid Id { get; set; }

        public Guid RequestId { get; set; }

        public RequestStatus Status { get; set; }

        public string? Comment { get; set; }

        public DateTime ChangedAt { get; set; }

        /// <summary>Кто сменил статус: студент или сотрудник. Пусто, если его учётную запись удалили.</summary>
        public Guid? ChangedByUserId { get; set; }
    }

    public sealed class RequestTypeConfiguration : IEntityTypeConfiguration<RequestType>
    {
        public void Configure( EntityTypeBuilder<RequestType> builder )
        {
            builder.ToTable( "request_types", table =>
                table.HasCheckConstraint( "ck_request_types_processing_days", "processing_days > 0" ) );
            builder.Property( type => type.Name ).HasMaxLength( 100 );
            builder.Property( type => type.Description ).HasMaxLength( 300 );
            builder.Property( type => type.FieldSchema ).HasColumnType( "jsonb" );
            builder.HasIndex( type => type.Name ).IsUnique();
            builder.HasData( RequestTypes.All );
        }
    }

    public sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
    {
        public void Configure( EntityTypeBuilder<Request> builder )
        {
            builder.ToTable( "requests", table =>
                table.HasCheckConstraint( "ck_requests_current_status", SqlIn( "current_status", Enum.GetNames<RequestStatus>() ) ) );
            builder.Property( request => request.CurrentStatus ).HasConversion<string>().HasMaxLength( 16 );
            builder.Property( request => request.Fields ).HasColumnType( "jsonb" );
            builder.Property( request => request.IssuePlace ).HasMaxLength( 200 );
            builder.Property( request => request.Version ).IsConcurrencyToken();

            // Список заявок студента, новые сверху, и очередь сотрудника по статусу.
            builder.HasIndex( request => new { request.StudentProfileId, request.CreatedAt } ).IsDescending( false, true );
            builder.HasIndex( request => new { request.CurrentStatus, request.CreatedAt } );

            builder.HasOne( request => request.StudentProfile ).WithMany().HasForeignKey( request => request.StudentProfileId )
                .HasConstraintName( "fk_requests_student_profiles_student_profile_id" ).OnDelete( DeleteBehavior.Cascade );
            builder.HasOne( request => request.Type ).WithMany().HasForeignKey( request => request.RequestTypeId )
                .HasConstraintName( "fk_requests_request_types_request_type_id" ).OnDelete( DeleteBehavior.Restrict );
        }

        internal static string SqlIn( string column, IEnumerable<string> values )
        {
            return $"{column} IN ({string.Join( ", ", values.Select( value => $"'{value}'" ) )})";
        }
    }

    public sealed class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
    {
        public void Configure( EntityTypeBuilder<RequestStatusHistory> builder )
        {
            builder.ToTable( "request_status_history", table =>
                table.HasCheckConstraint( "ck_request_status_history_status", RequestConfiguration.SqlIn( "status", Enum.GetNames<RequestStatus>() ) ) );
            builder.Property( entry => entry.Status ).HasConversion<string>().HasMaxLength( 16 );
            builder.Property( entry => entry.Comment ).HasMaxLength( 500 );
            builder.HasIndex( entry => new { entry.RequestId, entry.ChangedAt } );

            builder.HasOne<Request>().WithMany( request => request.History ).HasForeignKey( entry => entry.RequestId )
                .HasConstraintName( "fk_request_status_history_requests_request_id" ).OnDelete( DeleteBehavior.Cascade );
            builder.HasOne<AppUser>().WithMany().HasForeignKey( entry => entry.ChangedByUserId )
                .HasConstraintName( "fk_request_status_history_users_changed_by_user_id" ).OnDelete( DeleteBehavior.SetNull );
        }
    }
}
