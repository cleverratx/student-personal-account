namespace Dekanat.Api.Features.Requests
{
    /// <summary>
    /// Типы заявок, которые студент может подать. Справочник лежит в коде и попадает в базу миграцией,
    /// как роли: id постоянные, поэтому совпадают на всех базах. Новый тип или изменение схемы полей это новая миграция.
    /// Схема полей (<see cref="RequestType.FieldSchema"/>) это JSON вида { "поле": { "type", "required", "title", ... } }.
    /// Поддерживаются типы string, integer, date, uuid и choice (список options с value и title), ограничения
    /// maxLength для строки и min и max для числа. По схеме POST /api/requests проверяет значения полей.
    /// </summary>
    public static class RequestTypes
    {
        public static readonly Guid StudyCertificateId = new( "d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d01" );

        public static readonly Guid MilitaryCertificateId = new( "d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d02" );

        public static readonly Guid SocialCertificateId = new( "d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d03" );

        public static readonly Guid RetakeApplicationId = new( "d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d04" );

        public static readonly Guid AcademicLeaveApplicationId = new( "d7a1c3e5-4b2f-4a6d-9c8e-0f1a2b3c4d05" );

        private const string StudyCertificateSchema = """{"destination":{"type":"string","required":true,"title":"Куда предоставить","maxLength":200},"copies":{"type":"integer","required":true,"title":"Количество экземпляров","min":1,"max":5},"delivery":{"type":"choice","required":true,"title":"Как получить","options":[{"value":"Electronic","title":"Электронно"},{"value":"Deanery","title":"В деканате"}]},"comment":{"type":"string","required":false,"title":"Комментарий","maxLength":500}}""";

        private const string MilitaryCertificateSchema = """{"destination":{"type":"string","required":true,"title":"В какой военкомат","maxLength":200},"copies":{"type":"integer","required":true,"title":"Количество экземпляров","min":1,"max":5},"delivery":{"type":"choice","required":true,"title":"Как получить","options":[{"value":"Electronic","title":"Электронно"},{"value":"Deanery","title":"В деканате"}]},"comment":{"type":"string","required":false,"title":"Комментарий","maxLength":500}}""";

        private const string SocialCertificateSchema = """{"destination":{"type":"string","required":true,"title":"Куда предоставить","maxLength":200},"copies":{"type":"integer","required":true,"title":"Количество экземпляров","min":1,"max":5},"delivery":{"type":"choice","required":true,"title":"Как получить","options":[{"value":"Electronic","title":"Электронно"},{"value":"Deanery","title":"В деканате"}]},"comment":{"type":"string","required":false,"title":"Комментарий","maxLength":500}}""";

        private const string RetakeApplicationSchema = """{"assessmentId":{"type":"uuid","required":true,"title":"Дисциплина"},"comment":{"type":"string","required":false,"title":"Комментарий","maxLength":500}}""";

        private const string AcademicLeaveApplicationSchema = """{"reason":{"type":"choice","required":true,"title":"Причина","options":[{"value":"Health","title":"По состоянию здоровья"},{"value":"Family","title":"По семейным обстоятельствам"},{"value":"Other","title":"Другая причина"}]},"startDate":{"type":"date","required":true,"title":"С какой даты"},"durationMonths":{"type":"integer","required":true,"title":"На сколько месяцев","min":1,"max":12},"comment":{"type":"string","required":false,"title":"Комментарий","maxLength":500}}""";

        public static readonly IReadOnlyList<RequestType> All =
        [
            new()
            {
                Id = StudyCertificateId,
                Name = "Справка об обучении",
                Description = "Подтверждает, что вы учитесь в вузе",
                ProcessingDays = 3,
                FieldSchema = StudyCertificateSchema,
            },
            new()
            {
                Id = MilitaryCertificateId,
                Name = "Справка в военкомат",
                Description = "Справка для военного комиссариата",
                ProcessingDays = 5,
                FieldSchema = MilitaryCertificateSchema,
            },
            new()
            {
                Id = SocialCertificateId,
                Name = "Справка для соцзащиты или стипендии",
                Description = "Для органов соцзащиты или для назначения стипендии",
                ProcessingDays = 5,
                FieldSchema = SocialCertificateSchema,
            },
            new()
            {
                Id = RetakeApplicationId,
                Name = "Заявление на пересдачу",
                Description = "Заявление на пересдачу дисциплины",
                ProcessingDays = 2,
                FieldSchema = RetakeApplicationSchema,
            },
            new()
            {
                Id = AcademicLeaveApplicationId,
                Name = "Заявление на академический отпуск",
                Description = "Заявление на академический отпуск",
                ProcessingDays = 10,
                FieldSchema = AcademicLeaveApplicationSchema,
            },
        ];
    }
}
