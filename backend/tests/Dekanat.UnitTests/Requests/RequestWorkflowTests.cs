using Dekanat.Api.Features.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Dekanat.UnitTests.Requests
{
    public sealed class RequestWorkflowTests
    {
        private static readonly DateTime Now = new( 2026, 11, 18, 9, 0, 0, DateTimeKind.Utc );
        private static readonly Guid StudentUser = new( "11111111-1111-4111-8111-111111111111" );
        private static readonly Guid StaffUser = new( "22222222-2222-4222-8222-222222222222" );

        // Единственный список допустимых переходов, написанный независимо от кода. Всё, чего здесь нет, должно быть запрещено.
        private static readonly HashSet<(RequestStatus, RequestStatus, RequestActor)> Allowed =
        [
            (RequestStatus.Submitted, RequestStatus.InProgress, RequestActor.Staff),
            (RequestStatus.Submitted, RequestStatus.Rejected, RequestActor.Staff),
            (RequestStatus.Submitted, RequestStatus.Cancelled, RequestActor.Student),
            (RequestStatus.InProgress, RequestStatus.Ready, RequestActor.Staff),
            (RequestStatus.InProgress, RequestStatus.Rejected, RequestActor.Staff),
            (RequestStatus.Ready, RequestStatus.Issued, RequestActor.Staff),
        ];

        /// <summary>Все 6 x 6 x 2 сочетаний: из какого статуса, в какой и кто меняет.</summary>
        public static TheoryData<RequestStatus, RequestStatus, RequestActor, bool> AllTransitions()
        {
            TheoryData<RequestStatus, RequestStatus, RequestActor, bool> data = new();
            foreach ( RequestStatus from in Enum.GetValues<RequestStatus>() )
            {
                foreach ( RequestStatus to in Enum.GetValues<RequestStatus>() )
                {
                    foreach ( RequestActor actor in Enum.GetValues<RequestActor>() )
                    {
                        data.Add( from, to, actor, Allowed.Contains( (from, to, actor) ) );
                    }
                }
            }

            return data;
        }

        [Theory]
        [MemberData( nameof( AllTransitions ) )]
        public void EveryTransitionIsAllowedOnlyIfListed( RequestStatus from, RequestStatus to, RequestActor actor, bool allowed )
        {
            Request request = NewRequest( from );
            int version = request.Version;
            int historyCount = request.History.Count;

            TransitionResult result = RequestWorkflow.Apply( request, ValidChange( to, actor ), Now );

            Assert.Equal( allowed, RequestWorkflow.IsAllowed( from, to, actor ) );
            if ( allowed )
            {
                Assert.IsType<TransitionResult.Success>( result );
                Assert.Equal( to, request.CurrentStatus );
                Assert.Equal( version + 1, request.Version );
                Assert.Equal( historyCount + 1, request.History.Count );
            }
            else
            {
                TransitionResult.NotAllowed conflict = Assert.IsType<TransitionResult.NotAllowed>( result );
                Assert.Equal( from, conflict.From );
                Assert.Equal( to, conflict.To );

                // Запрещённый переход ничего не меняет: ни статус, ни версию, ни историю.
                Assert.Equal( from, request.CurrentStatus );
                Assert.Equal( version, request.Version );
                Assert.Equal( historyCount, request.History.Count );
            }
        }

        [Fact]
        public void NotAllowedTransitionIsConflict()
        {
            Request request = NewRequest( RequestStatus.Issued );

            TransitionResult result = RequestWorkflow.Apply( request, ValidChange( RequestStatus.InProgress, RequestActor.Staff ), Now );

            ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>( result.ToProblem() );
            Assert.Equal( StatusCodes.Status409Conflict, problem.StatusCode );
            Assert.Contains( "Выдана", problem.ProblemDetails.Detail, StringComparison.Ordinal );
        }

        [Fact]
        public void StudentCannotCancelOnceInProgress()
        {
            foreach ( RequestStatus status in new[] { RequestStatus.InProgress, RequestStatus.Ready, RequestStatus.Issued, RequestStatus.Rejected, RequestStatus.Cancelled } )
            {
                Request request = NewRequest( status );

                TransitionResult result = RequestWorkflow.Apply( request, ValidChange( RequestStatus.Cancelled, RequestActor.Student ), Now );

                ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>( result.ToProblem() );
                Assert.Equal( StatusCodes.Status409Conflict, problem.StatusCode );
                Assert.Contains( "Подана", problem.ProblemDetails.Detail, StringComparison.Ordinal );
            }
        }

        [Fact]
        public void StudentCanCancelSubmittedRequest()
        {
            Request request = NewRequest( RequestStatus.Submitted );

            TransitionResult result = RequestWorkflow.Apply( request, ValidChange( RequestStatus.Cancelled, RequestActor.Student ), Now );

            Assert.Null( result.ToProblem() );
            Assert.Equal( RequestStatus.Cancelled, request.CurrentStatus );
        }

        [Fact]
        public void FullPathToIssuedWritesHistoryWithAuthorAndTime()
        {
            Request request = NewRequest( RequestStatus.Submitted );
            DateTime deadline = Now.AddDays( 5 );

            RequestWorkflow.Apply( request, new StatusChange( RequestStatus.InProgress, RequestActor.Staff, StaffUser, "Передана в деканат" ), Now.AddHours( 1 ) );
            RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Ready, RequestActor.Staff, StaffUser, null, "  Деканат, кабинет 204  ", deadline ), Now.AddHours( 2 ) );
            RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Issued, RequestActor.Staff, StaffUser ), Now.AddHours( 3 ) );

            Assert.Equal( RequestStatus.Issued, request.CurrentStatus );
            Assert.Equal( "Деканат, кабинет 204", request.IssuePlace );
            Assert.Equal( deadline, request.IssueDeadline );
            Assert.Equal( 3, request.Version );
            Assert.Equal(
                new[] { RequestStatus.Submitted, RequestStatus.InProgress, RequestStatus.Ready, RequestStatus.Issued },
                request.History.Select( entry => entry.Status ) );
            Assert.Equal( new Guid?[] { StudentUser, StaffUser, StaffUser, StaffUser }, request.History.Select( entry => ( Guid? )entry.ChangedByUserId ) );
            Assert.Equal( new[] { Now, Now.AddHours( 1 ), Now.AddHours( 2 ), Now.AddHours( 3 ) }, request.History.Select( entry => entry.ChangedAt ) );
            Assert.Equal( "Передана в деканат", request.History[ 1 ].Comment );
            Assert.All( request.History, entry => Assert.Equal( request.Id, entry.RequestId ) );
        }

        [Fact]
        public void RejectionNeedsReasonAndKeepsItInHistory()
        {
            Request request = NewRequest( RequestStatus.Submitted );

            TransitionResult withoutReason = RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Rejected, RequestActor.Staff, StaffUser, "   " ), Now );

            ValidationProblem invalid = Assert.IsType<ValidationProblem>( withoutReason.ToProblem() );
            Assert.Contains( "comment", invalid.ProblemDetails.Errors.Keys );
            Assert.Equal( RequestStatus.Submitted, request.CurrentStatus );
            Assert.Single( request.History );

            TransitionResult withReason = RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Rejected, RequestActor.Staff, StaffUser, " Нет оснований " ), Now );

            Assert.IsType<TransitionResult.Success>( withReason );
            Assert.Equal( RequestStatus.Rejected, request.CurrentStatus );
            Assert.Equal( "Нет оснований", request.History[ ^1 ].Comment );
        }

        [Fact]
        public void ReadyNeedsPlaceAndFutureDeadline()
        {
            Request request = NewRequest( RequestStatus.InProgress );

            TransitionResult empty = RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Ready, RequestActor.Staff, StaffUser ), Now );
            TransitionResult past = RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Ready, RequestActor.Staff, StaffUser, null, "Деканат", Now ), Now );

            TransitionResult.Invalid emptyErrors = Assert.IsType<TransitionResult.Invalid>( empty );
            Assert.Contains( "issuePlace", emptyErrors.Errors.Keys );
            Assert.Contains( "issueDeadline", emptyErrors.Errors.Keys );
            TransitionResult.Invalid pastErrors = Assert.IsType<TransitionResult.Invalid>( past );
            Assert.Equal( new[] { "issueDeadline" }, pastErrors.Errors.Keys );
            Assert.Equal( RequestStatus.InProgress, request.CurrentStatus );
            Assert.Null( request.IssuePlace );
            Assert.Single( request.History );
        }

        [Fact]
        public void NotAllowedIsCheckedBeforeData()
        {
            // Из «Выдана» в «Готова» без места выдачи: сначала 409, а не 400.
            Request request = NewRequest( RequestStatus.Issued );

            TransitionResult result = RequestWorkflow.Apply( request, new StatusChange( RequestStatus.Ready, RequestActor.Staff, StaffUser ), Now );

            Assert.IsType<TransitionResult.NotAllowed>( result );
        }

        [Fact]
        public void TooLongCommentIsRejected()
        {
            Request request = NewRequest( RequestStatus.Submitted );
            string comment = new( 'а', RequestWorkflow.MaxCommentLength + 1 );

            TransitionResult result = RequestWorkflow.Apply( request, new StatusChange( RequestStatus.InProgress, RequestActor.Staff, StaffUser, comment ), Now );

            Assert.IsType<TransitionResult.Invalid>( result );
        }

        [Theory]
        [InlineData( RequestStatus.Submitted, RequestActor.Student, new[] { RequestStatus.Cancelled } )]
        [InlineData( RequestStatus.Submitted, RequestActor.Staff, new[] { RequestStatus.InProgress, RequestStatus.Rejected } )]
        [InlineData( RequestStatus.InProgress, RequestActor.Staff, new[] { RequestStatus.Ready, RequestStatus.Rejected } )]
        [InlineData( RequestStatus.InProgress, RequestActor.Student, new RequestStatus[ 0 ] )]
        [InlineData( RequestStatus.Ready, RequestActor.Staff, new[] { RequestStatus.Issued } )]
        [InlineData( RequestStatus.Issued, RequestActor.Staff, new RequestStatus[ 0 ] )]
        [InlineData( RequestStatus.Rejected, RequestActor.Staff, new RequestStatus[ 0 ] )]
        [InlineData( RequestStatus.Cancelled, RequestActor.Student, new RequestStatus[ 0 ] )]
        public void NextStatusesListWhatCanBeDone( RequestStatus from, RequestActor actor, RequestStatus[] expected )
        {
            Assert.Equal( expected.OrderBy( status => status ), RequestWorkflow.NextStatuses( from, actor ).OrderBy( status => status ) );
        }

        [Theory]
        [InlineData( RequestStatus.Issued, true )]
        [InlineData( RequestStatus.Rejected, true )]
        [InlineData( RequestStatus.Cancelled, true )]
        [InlineData( RequestStatus.Submitted, false )]
        [InlineData( RequestStatus.InProgress, false )]
        [InlineData( RequestStatus.Ready, false )]
        public void FinalStatusesHaveNoWayOut( RequestStatus status, bool isFinal )
        {
            Assert.Equal( isFinal, RequestWorkflow.IsFinal( status ) );
        }

        [Fact]
        public void SubmitCreatesSubmittedRequestWithFirstHistoryEntry()
        {
            RequestType type = RequestTypes.All[ 0 ];
            Guid profileId = Guid.NewGuid();

            Request request = RequestWorkflow.Submit( profileId, StudentUser, type, "{}", Now, new DateOnly( 2026, 11, 18 ) );

            Assert.NotEqual( Guid.Empty, request.Id );
            Assert.Equal( profileId, request.StudentProfileId );
            Assert.Equal( type.Id, request.RequestTypeId );
            Assert.Equal( RequestStatus.Submitted, request.CurrentStatus );
            Assert.Equal( Now, request.CreatedAt );
            Assert.Equal( new DateOnly( 2026, 11, 23 ), request.EstimatedReadyDate );
            RequestStatusHistory first = Assert.Single( request.History );
            Assert.Equal( RequestStatus.Submitted, first.Status );
            Assert.Equal( StudentUser, first.ChangedByUserId );
        }

        [Theory]
        [InlineData( "2026-11-18", 3, "2026-11-23" )] // среда + 3 рабочих дня = понедельник
        [InlineData( "2026-11-20", 2, "2026-11-24" )] // пятница + 2 = вторник
        [InlineData( "2026-11-21", 1, "2026-11-23" )] // суббота + 1 = понедельник
        [InlineData( "2026-11-18", 10, "2026-12-02" )]
        [InlineData( "2026-11-18", 0, "2026-11-18" )]
        public void ReadyDateSkipsWeekends( string from, int days, string expected )
        {
            Assert.Equal( DateOnly.Parse( expected ), RequestWorkflow.EstimateReadyDate( DateOnly.Parse( from ), days ) );
        }

        [Theory]
        [InlineData( RequestStatus.Submitted, "Подана" )]
        [InlineData( RequestStatus.InProgress, "В работе" )]
        [InlineData( RequestStatus.Ready, "Готова" )]
        [InlineData( RequestStatus.Issued, "Выдана" )]
        [InlineData( RequestStatus.Rejected, "Отклонена" )]
        [InlineData( RequestStatus.Cancelled, "Отменена" )]
        public void EveryStatusHasRussianTitle( RequestStatus status, string title )
        {
            Assert.Equal( title, RequestWorkflow.Title( status ) );
        }

        private static Request NewRequest( RequestStatus status )
        {
            Request request = RequestWorkflow.Submit( Guid.NewGuid(), StudentUser, RequestTypes.All[ 0 ], "{}", Now, new DateOnly( 2026, 11, 18 ) );
            request.CurrentStatus = status;
            return request;
        }

        /// <summary>Изменение со всеми данными, нужными для целевого статуса: проверяем сам переход, а не поля.</summary>
        private static StatusChange ValidChange( RequestStatus to, RequestActor actor )
        {
            return new StatusChange(
                to,
                actor,
                actor == RequestActor.Student ? StudentUser : StaffUser,
                "Комментарий",
                "Деканат, кабинет 204",
                Now.AddDays( 3 ) );
        }
    }
}
