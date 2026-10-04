namespace AmharcAgent.Api.W1;

public static class W1DevelopmentServices
{
    public static IServiceCollection AddW1DevelopmentComposition(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        // Two independent gates. No private fixture key or authority is shipped/enabled.
        if (environment.IsDevelopment() && configuration.GetValue<bool>("W1:DevelopmentOnly"))
            services.AddSingleton<W1DevelopmentApplication>();
        return services;
    }
}