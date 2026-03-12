using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClawCage.WinUI.Components
{
    public sealed partial class AddModelWizardModelStep : UserControl
    {
        private bool _tested;

        public AddModelWizardModelStep()
        {
            InitializeComponent();
        }

        internal string ModelId => ModelIdBox.Text.Trim();
        internal int ContextWindow => double.IsNaN(ContextWindowBox.Value) ? 0 : (int)ContextWindowBox.Value;
        internal int MaxTokens => double.IsNaN(MaxTokensBox.Value) ? 0 : (int)MaxTokensBox.Value;

        internal bool Validate()
        {
            if (!_tested)
            {
                TestText.Text = "请先测试通过。";
                return false;
            }
            if (ContextWindowBox.Value <= 0 || double.IsNaN(ContextWindowBox.Value))
            {
                TestText.Text = "ContextWindow 必须填写且大于 0。";
                return false;
            }
            if (MaxTokensBox.Value <= 0 || double.IsNaN(MaxTokensBox.Value))
            {
                TestText.Text = "MaxTokens 必须填写且大于 0。";
                return false;
            }
            return true;
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ModelIdBox.Text))
            {
                _tested = false;
                TestText.Text = "测试失败：Model ID 不能为空。";
                return;
            }

            _tested = true;
            TestText.Text = "测试通过。";
        }
    }
}
