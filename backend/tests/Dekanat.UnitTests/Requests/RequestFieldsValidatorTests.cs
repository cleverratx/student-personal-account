using System.Text.Json;
using Dekanat.Api.Features.Requests;

namespace Dekanat.UnitTests.Requests
{
    public sealed class RequestFieldsValidatorTests
    {
        private static string SchemaOf( Guid typeId )
        {
            return RequestTypes.All.Single( type => type.Id == typeId ).FieldSchema;
        }

        private static FieldsValidation Validate( Guid typeId, string json )
        {
            using JsonDocument document = JsonDocument.Parse( json );
            return RequestFieldsValidator.Validate( document.RootElement.Clone(), SchemaOf( typeId ) );
        }

        [Fact]
        public void ValidCertificateIsAcceptedAndCleaned()
        {
            FieldsValidation result = Validate(
                RequestTypes.StudyCertificateId,
                """{"destination":"  В банк  ","copies":2,"delivery":"Deanery","comment":""}""" );

            Assert.Empty( result.Errors );
            using JsonDocument saved = JsonDocument.Parse( result.Json! );
            Assert.Equal( "В банк", saved.RootElement.GetProperty( "destination" ).GetString() );
            Assert.Equal( 2, saved.RootElement.GetProperty( "copies" ).GetInt32() );
            Assert.Equal( "Deanery", saved.RootElement.GetProperty( "delivery" ).GetString() );

            // Пустой необязательный комментарий не сохраняется.
            Assert.False( saved.RootElement.TryGetProperty( "comment", out _ ) );
        }

        [Fact]
        public void MissingRequiredFieldsAreReportedByName()
        {
            FieldsValidation result = Validate( RequestTypes.StudyCertificateId, """{"destination":"   "}""" );

            Assert.Null( result.Json );
            Assert.Equal( new[] { "fields.copies", "fields.delivery", "fields.destination" }, result.Errors.Keys.Order() );
            Assert.Contains( "Куда предоставить", result.Errors[ "fields.destination" ][ 0 ], StringComparison.Ordinal );
        }

        [Theory]
        [InlineData( """{"destination":"x","copies":0,"delivery":"Deanery"}""", "fields.copies" )]
        [InlineData( """{"destination":"x","copies":6,"delivery":"Deanery"}""", "fields.copies" )]
        [InlineData( """{"destination":"x","copies":1.5,"delivery":"Deanery"}""", "fields.copies" )]
        [InlineData( """{"destination":"x","copies":"2","delivery":"Deanery"}""", "fields.copies" )]
        [InlineData( """{"destination":"x","copies":1,"delivery":"Post"}""", "fields.delivery" )]
        [InlineData( """{"destination":"x","copies":1,"delivery":1}""", "fields.delivery" )]
        [InlineData( """{"destination":123,"copies":1,"delivery":"Deanery"}""", "fields.destination" )]
        [InlineData( """{"destination":"x","copies":1,"delivery":"Deanery","comment":["a"]}""", "fields.comment" )]
        public void WrongValuesAreRejected( string json, string errorKey )
        {
            FieldsValidation result = Validate( RequestTypes.StudyCertificateId, json );

            Assert.Equal( new[] { errorKey }, result.Errors.Keys );
        }

        [Fact]
        public void TooLongTextIsRejected()
        {
            string longText = new( 'а', 201 );

            FieldsValidation result = Validate(
                RequestTypes.StudyCertificateId,
                $$"""{"destination":"{{longText}}","copies":1,"delivery":"Deanery"}""" );

            Assert.Equal( new[] { "fields.destination" }, result.Errors.Keys );
        }

        [Fact]
        public void UnknownFieldsAreRejected()
        {
            FieldsValidation result = Validate(
                RequestTypes.StudyCertificateId,
                """{"destination":"x","copies":1,"delivery":"Deanery","studentId":"1","status":"Issued"}""" );

            Assert.Equal( new[] { "fields" }, result.Errors.Keys );
            Assert.Contains( "studentId", result.Errors[ "fields" ][ 0 ], StringComparison.Ordinal );
            Assert.Contains( "status", result.Errors[ "fields" ][ 0 ], StringComparison.Ordinal );
        }

        [Theory]
        [InlineData( "[]" )]
        [InlineData( "\"text\"" )]
        [InlineData( "null" )]
        [InlineData( "5" )]
        public void FieldsMustBeAnObject( string json )
        {
            FieldsValidation result = Validate( RequestTypes.StudyCertificateId, json );

            Assert.Equal( new[] { "fields" }, result.Errors.Keys );
        }

        [Fact]
        public void UndefinedFieldsValueIsRejected()
        {
            // Тело запроса без поля fields: JsonElement остаётся пустым.
            FieldsValidation result = RequestFieldsValidator.Validate( default, SchemaOf( RequestTypes.StudyCertificateId ) );

            Assert.Equal( new[] { "fields" }, result.Errors.Keys );
        }

        [Fact]
        public void RetakeNeedsAssessmentId()
        {
            Guid assessment = Guid.NewGuid();

            FieldsValidation ok = Validate( RequestTypes.RetakeApplicationId, $$"""{"assessmentId":"{{assessment}}"}""" );
            FieldsValidation notUuid = Validate( RequestTypes.RetakeApplicationId, """{"assessmentId":"abc"}""" );
            FieldsValidation empty = Validate( RequestTypes.RetakeApplicationId, $$"""{"assessmentId":"{{Guid.Empty}}"}""" );
            FieldsValidation missing = Validate( RequestTypes.RetakeApplicationId, "{}" );

            Assert.Empty( ok.Errors );
            Assert.Equal( new[] { "fields.assessmentId" }, notUuid.Errors.Keys );
            Assert.Equal( new[] { "fields.assessmentId" }, empty.Errors.Keys );
            Assert.Equal( new[] { "fields.assessmentId" }, missing.Errors.Keys );
        }

        [Fact]
        public void AcademicLeaveChecksReasonDateAndMonths()
        {
            FieldsValidation ok = Validate(
                RequestTypes.AcademicLeaveApplicationId,
                """{"reason":"Health","startDate":"2027-02-01","durationMonths":6}""" );
            FieldsValidation bad = Validate(
                RequestTypes.AcademicLeaveApplicationId,
                """{"reason":"Boredom","startDate":"01.02.2027","durationMonths":13}""" );

            Assert.Empty( ok.Errors );
            Assert.Equal( new[] { "fields.durationMonths", "fields.reason", "fields.startDate" }, bad.Errors.Keys.Order() );
        }

        [Fact]
        public void EveryTypeAcceptsItsOwnMinimalValidFields()
        {
            Dictionary<Guid, string> valid = new()
            {
                [ RequestTypes.StudyCertificateId ] = """{"destination":"x","copies":1,"delivery":"Electronic"}""",
                [ RequestTypes.MilitaryCertificateId ] = """{"destination":"x","copies":1,"delivery":"Electronic"}""",
                [ RequestTypes.SocialCertificateId ] = """{"destination":"x","copies":1,"delivery":"Deanery"}""",
                [ RequestTypes.RetakeApplicationId ] = $$"""{"assessmentId":"{{Guid.NewGuid()}}"}""",
                [ RequestTypes.AcademicLeaveApplicationId ] = """{"reason":"Other","startDate":"2027-02-01","durationMonths":1}""",
            };

            Assert.Equal( RequestTypes.All.Select( type => type.Id ).Order(), valid.Keys.Order() );
            Assert.All( valid, pair => Assert.Empty( Validate( pair.Key, pair.Value ).Errors ) );
        }
    }
}
