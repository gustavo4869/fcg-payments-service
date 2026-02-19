using Fcg.Payments.Api.Infra.Messaging;
using Microsoft.Extensions.Options;

namespace Fcg.Payments.Api.Setup
{
    public class MessagingOptionsValidator : IValidateOptions<MessagingOptions>
    {
        public ValidateOptionsResult Validate(string? name, MessagingOptions options)
        {
            // Se messaging está desabilitado, não precisa validar
            if (!options.Enabled)
            {
                return ValidateOptionsResult.Success;
            }

            // Se está habilitado, validar campos obrigatórios
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                errors.Add("Messaging:Host is required when Messaging:Enabled is true");
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                errors.Add("Messaging:Port must be between 1 and 65535");
            }

            if (string.IsNullOrWhiteSpace(options.Username))
            {
                errors.Add("Messaging:Username is required when Messaging:Enabled is true");
            }

            if (string.IsNullOrWhiteSpace(options.Password))
            {
                errors.Add("Messaging:Password is required when Messaging:Enabled is true");
            }

            if (string.IsNullOrWhiteSpace(options.VHost))
            {
                errors.Add("Messaging:VHost is required when Messaging:Enabled is true");
            }

            if (string.IsNullOrWhiteSpace(options.Exchange))
            {
                errors.Add("Messaging:Exchange is required when Messaging:Enabled is true");
            }

            if (string.IsNullOrWhiteSpace(options.RoutingKey))
            {
                errors.Add("Messaging:RoutingKey is required when Messaging:Enabled is true");
            }

            if (errors.Any())
            {
                return ValidateOptionsResult.Fail(errors);
            }

            return ValidateOptionsResult.Success;
        }
    }
}
