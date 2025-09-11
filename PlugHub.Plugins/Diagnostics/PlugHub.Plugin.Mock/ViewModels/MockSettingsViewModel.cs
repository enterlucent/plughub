using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace PlugHub.Plugin.Mock.ViewModels
{
    public partial class MockSettingsViewModel : BaseViewModel
    {
        private readonly IConfigAccessorFor<PluginMockConfig> accessor;
        private int answerToEverything = 42;

        [CustomValidation(typeof(MockSettingsViewModel), nameof(ValidateAnswer))]
        public int AnswerToEverything
        {
            get => this.answerToEverything;
            set
            {
                if (this.SetProperty(ref this.answerToEverything, value, validate: true))
                {
                    this.accessor.Set("AnswerToEverything", value);
                    this.accessor.Save();
                }
            }
        }

        public Func<object, object> ExceptionConverter { get; } = new Func<object, object>(o =>
        {
            if (o is Exception ex)
            {
                return "Invalid Value";
            }
            else
            {
                return o;
            }
        });


        public MockSettingsViewModel(IConfigAccessorFor<PluginMockConfig> accessor)
        {
            this.accessor = accessor;
            this.AnswerToEverything = accessor.Get<int>(nameof(this.AnswerToEverything));
        }

        public static ValidationResult? ValidateAnswer(string? value, ValidationContext context)
        {
            if (value != "42")
                return new ValidationResult("I'm afraid that's not quite right—but don't panic, even the universe didn't have all the answers at once.", [nameof(AnswerToEverything)]);

            return ValidationResult.Success;
        }
    }
}