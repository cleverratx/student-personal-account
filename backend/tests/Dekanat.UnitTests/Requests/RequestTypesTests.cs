using System.Text.Json;
using Dekanat.Api.Features.Requests;

namespace Dekanat.UnitTests.Requests
{
    public sealed class RequestTypesTests
    {
        private static readonly string[] KnownFieldTypes = [ "string", "integer", "date", "uuid", "choice" ];

        [Fact]
        public void HasFiveTypesWithUniqueIdsAndNames()
        {
            Assert.Equal( 5, RequestTypes.All.Count );
            Assert.Equal( 5, RequestTypes.All.Select( type => type.Id ).Distinct().Count() );
            Assert.Equal( 5, RequestTypes.All.Select( type => type.Name ).Distinct().Count() );
            Assert.All( RequestTypes.All, type => Assert.True( type.ProcessingDays > 0 ) );
        }

        [Fact]
        public void EveryTypeHasValidFieldSchema()
        {
            foreach ( RequestType type in RequestTypes.All )
            {
                using JsonDocument schema = JsonDocument.Parse( type.FieldSchema );
                Assert.Equal( JsonValueKind.Object, schema.RootElement.ValueKind );
                Assert.NotEmpty( schema.RootElement.EnumerateObject() );

                foreach ( JsonProperty field in schema.RootElement.EnumerateObject() )
                {
                    Assert.Contains( field.Value.GetProperty( "type" ).GetString(), KnownFieldTypes );
                    Assert.True( field.Value.GetProperty( "required" ).ValueKind is JsonValueKind.True or JsonValueKind.False );
                    Assert.False( string.IsNullOrWhiteSpace( field.Value.GetProperty( "title" ).GetString() ) );

                    if ( field.Value.GetProperty( "type" ).GetString() == "choice" )
                    {
                        Assert.NotEmpty( field.Value.GetProperty( "options" ).EnumerateArray() );
                    }
                }
            }
        }

        [Theory]
        [InlineData( "Справка об обучении" )]
        [InlineData( "Справка в военкомат" )]
        [InlineData( "Справка для соцзащиты или стипендии" )]
        public void CertificatesAskWhereHowManyAndHowToReceive( string name )
        {
            RequestType type = RequestTypes.All.Single( item => item.Name == name );
            using JsonDocument schema = JsonDocument.Parse( type.FieldSchema );

            string[] fields = [ .. schema.RootElement.EnumerateObject().Select( field => field.Name ) ];

            Assert.Equal( new[] { "destination", "copies", "delivery", "comment" }, fields );
            Assert.True( schema.RootElement.GetProperty( "destination" ).GetProperty( "required" ).GetBoolean() );
            Assert.False( schema.RootElement.GetProperty( "comment" ).GetProperty( "required" ).GetBoolean() );
            string[] delivery = [ .. schema.RootElement.GetProperty( "delivery" ).GetProperty( "options" ).EnumerateArray().Select( option => option.GetProperty( "value" ).GetString()! ) ];
            Assert.Equal( new[] { "Electronic", "Deanery" }, delivery );
        }

        [Fact]
        public void RetakeNeedsAssessmentAndLeaveNeedsReasonAndDates()
        {
            using JsonDocument retake = JsonDocument.Parse( RequestTypes.All.Single( type => type.Id == RequestTypes.RetakeApplicationId ).FieldSchema );
            using JsonDocument leave = JsonDocument.Parse( RequestTypes.All.Single( type => type.Id == RequestTypes.AcademicLeaveApplicationId ).FieldSchema );

            Assert.True( retake.RootElement.GetProperty( "assessmentId" ).GetProperty( "required" ).GetBoolean() );
            Assert.Equal( "uuid", retake.RootElement.GetProperty( "assessmentId" ).GetProperty( "type" ).GetString() );
            Assert.True( leave.RootElement.GetProperty( "reason" ).GetProperty( "required" ).GetBoolean() );
            Assert.True( leave.RootElement.GetProperty( "startDate" ).GetProperty( "required" ).GetBoolean() );
            Assert.True( leave.RootElement.GetProperty( "durationMonths" ).GetProperty( "required" ).GetBoolean() );
        }
    }
}
