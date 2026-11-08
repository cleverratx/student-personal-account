using System.Reflection;

namespace Dekanat.Api.Infrastructure.OpenApi
{
    public static class OpenApiSetup
    {
        public static IServiceCollection AddDekanatOpenApi( this IServiceCollection services )
        {
            return services.AddOpenApi( options => options.AddDocumentTransformer( ( document, _, _ ) =>
            {
                document.Info.Title = "Цифровой деканат API";
                document.Info.Description =
                    "Концепт для MGIMO AI Challenge. Не официальный сервис вуза, все данные вымышленные.";
                return Task.CompletedTask;
            } ) );
        }

        /// <summary>
        /// При сборке OpenAPI-документа приложение запускает инструмент GetDocument.Insider, без сервера и без базы.
        /// В этом режиме пропускаем всё, что ходит наружу, например миграции.
        /// </summary>
        public static bool IsGeneratingDocument =>
            Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }
}
