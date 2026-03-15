using System.Collections.Generic;

namespace ClawCage.WinUI.Components.Integrations
{
    internal sealed class LarkIntegrationWizardComponent : IIntegrationWizardComponent
    {
        public string Key => "feishu";
        public string ConfigKey => "feishu";
        public string Title => "飞书";
        public string Description => "通过飞书自定义机器人发送消息与通知";
        public string Glyph => "\uE8C1";
        public string? IconResourceName => "lark";
        public string NpmPackageName => "@larksuiteoapi/feishu-openclaw-plugin";

        public IReadOnlyList<ChannelConfigField> ConfigFields { get; } =
        [
            new() { Name = "appId",          Label = "App ID",        Hint = "飞书应用的 App ID，如 cli_你的AppID",  DefaultValue = "",     FieldType = ChannelConfigFieldType.String, Required = true },
            new() { Name = "appSecret",      Label = "App Secret",    Hint = "飞书应用的 App Secret",                DefaultValue = "",     FieldType = ChannelConfigFieldType.String, Required = true },
            new() { Name = "requireMention", Label = "需要 @",        Hint = "是否需要 @ 机器人才响应: open / close",  DefaultValue = "open", FieldType = ChannelConfigFieldType.String },
            new() { Name = "groupPolicy",    Label = "群聊策略",       Hint = "群聊消息策略: open / close",            DefaultValue = "open", FieldType = ChannelConfigFieldType.String },
        ];
    }
}
