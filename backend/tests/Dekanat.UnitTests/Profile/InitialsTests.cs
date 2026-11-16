using Dekanat.Api.Features.Profile;

namespace Dekanat.UnitTests.Profile
{
    public sealed class InitialsTests
    {
        [Theory]
        [InlineData( "Иванова Анастасия Сергеевна", "АИ" )]
        [InlineData( "Ким Денис", "ДК" )]
        [InlineData( "Платон", "П" )]
        [InlineData( "  крылова   ольга  ", "ОК" )]
        [InlineData( "", "" )]
        public void InitialsAreFirstNameThenSurname( string fullName, string expected )
        {
            Assert.Equal( expected, ProfileEndpoints.InitialsOf( fullName ) );
        }
    }
}
