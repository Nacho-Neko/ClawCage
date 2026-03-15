using System;
using System.Collections.Generic;

namespace ClawCage.WinUI.Components.Integrations
{
    internal sealed class DingTalkIntegrationWizardComponent : IIntegrationWizardComponent
    {
        public string Key => "dingtalk";
        public string Title => "钉钉";
        public string Description => "通过钉钉自定义机器人发送消息与通知";
        public string Glyph => "\uE8BD";
        public string? IconResourceName => "dingtalk";
        public string NpmPackageName => "@soimy/dingtalk";

        public IReadOnlyList<ChannelConfigField> ConfigFields { get; } =
        [
            new() { Name = "clientId",         Label = "Client ID",        Hint = "钉钉应用的 AppKey / Client ID，如 dingxxxxxx",   DefaultValue = "",          FieldType = ChannelConfigFieldType.String, Required = true },
            new() { Name = "clientSecret",     Label = "Client Secret",    Hint = "钉钉应用的 AppSecret",                            DefaultValue = "",          FieldType = ChannelConfigFieldType.String, Required = true },
            new() { Name = "robotCode",        Label = "Robot Code",       Hint = "机器人的 robotCode，如 dingxxxxxx",                DefaultValue = "",          FieldType = ChannelConfigFieldType.String, Required = true },
            new() { Name = "corpId",           Label = "Corp ID",          Hint = "企业的 corpId，如 dingxxxxxx",                     DefaultValue = "",          FieldType = ChannelConfigFieldType.String, Required = true },
            new() { Name = "agentId",          Label = "Agent ID",         Hint = "应用的 AgentId，纯数字",                            DefaultValue = "",          FieldType = ChannelConfigFieldType.String },
            new() { Name = "dmPolicy",         Label = "私聊策略",          Hint = "私聊消息策略: open / close",                       DefaultValue = "open",      FieldType = ChannelConfigFieldType.String },
            new() { Name = "groupPolicy",      Label = "群聊策略",          Hint = "群聊消息策略: open / close",                       DefaultValue = "open",      FieldType = ChannelConfigFieldType.String },
            new() { Name = "allowFrom",        Label = "允许的发送者",        Hint = "允许的发送者 ID 列表，每行一个",                       DefaultValue = Array.Empty<string>(), FieldType = ChannelConfigFieldType.StringArray },
            new() { Name = "mediaUrlAllowlist", Label = "媒体 URL 白名单",    Hint = "允许通过 mediaUrl 下载的主机/IP/CIDR，每行一个",      DefaultValue = Array.Empty<string>(), FieldType = ChannelConfigFieldType.StringArray },
            new() { Name = "journalTTLDays",   Label = "日志保留天数",       Hint = "会话日志保留天数",                                  DefaultValue = 7,           FieldType = ChannelConfigFieldType.Int },
            new() { Name = "showThinking",     Label = "显示思考过程",       Hint = "仅 markdown 模式生效",                             DefaultValue = true,        FieldType = ChannelConfigFieldType.Bool },
            new() { Name = "thinkingMessage",  Label = "思考提示消息",       Hint = "仅 markdown 模式生效；设为 \"emoji\" 可启用随机颜文字彩蛋", DefaultValue = "🤔 思考中，请稍候...", FieldType = ChannelConfigFieldType.String },
            new() { Name = "debug",            Label = "调试模式",          Hint = "启用后输出详细日志",                                 DefaultValue = false,       FieldType = ChannelConfigFieldType.Bool },
            new() { Name = "messageType",      Label = "消息类型",          Hint = "消息渲染模式",                                      DefaultValue = "markdown",  FieldType = ChannelConfigFieldType.Combo, ComboOptions = ["markdown", "card"] },
            new() { Name = "cardTemplateId",   Label = "卡片模板 ID",       Hint = "从钉钉开放平台复制的模板 ID",                        DefaultValue = "",          FieldType = ChannelConfigFieldType.String, VisibleWhen = "messageType", VisibleWhenValue = "card" },
            new() { Name = "cardTemplateKey",  Label = "卡片模板变量名",     Hint = "模板中的内容变量 Key",                               DefaultValue = "",          FieldType = ChannelConfigFieldType.String, VisibleWhen = "messageType", VisibleWhenValue = "card" },
            new() { Name = "mediaMaxMb",       Label = "文件大小上限 (MB)",  Hint = "接收文件大小上限，默认 5 MB",                         DefaultValue = 5,           FieldType = ChannelConfigFieldType.Int },
            new() { Name = "aicardDegradeMs",  Label = "AI 卡片降级时间 (ms)", Hint = "AI 卡片失败后降级持续时间，默认 1800000（30 分钟）", DefaultValue = 1800000,     FieldType = ChannelConfigFieldType.Int },

        ];
    }
}
