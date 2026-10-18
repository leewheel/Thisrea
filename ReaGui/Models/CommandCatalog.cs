namespace ReaGui.Models;

/// <summary>
/// GUI 暴露的命令清单（元数据驱动，可扩展）。
/// 依据 REA CLI 契约：node scripts/rea.mjs &lt;command&gt; &lt;path&gt; [args] --json --full-output
/// By leewheel 2026-10-16
/// </summary>
public static class CommandCatalog
{
    public static readonly string[] Providers = { "auto", "ghidra", "hopper", "ida", "kuna" };
    public static readonly string[] DoctorProviders = { "hopper", "ghidra", "ida", "jadx", "kuna" };

    // ---- 复用参数工厂 ----
    private static CommandParameter Path() => new()
    {
        Name = "path", Label = "目标文件", Type = ArgType.FilePath, Required = true, Position = 0,
        Hint = "待逆向的可执行文件 / 库 / 分析数据库路径（第一个位置参数）",
    };

    private static CommandParameter Provider() => new()
    {
        Name = "provider", Label = "分析引擎", Type = ArgType.Enum, Options = Providers, Default = "auto",
        Hint = "深度分析引擎；auto 让 REA 自动选择",
    };

    private static CommandParameter Snapshot() => new()
    {
        Name = "snapshot", Label = "快照 (可选)", Type = ArgType.FilePath,
        Hint = "--snapshot：复用/更新本地分析快照数据库以加速",
    };

    private static CommandParameter Selector(string name, string label, int pos, string flag, string hint) => new()
    {
        Name = flag, Label = label, Type = ArgType.Text, Position = pos,
        Hint = hint + $"（也可用 --{flag}= 传值）",
    };

    public static readonly IReadOnlyList<ReaCommand> All = new List<ReaCommand>
    {
        // ============ 核心分析 ============
        new()
        {
            Name = "analyze", Title = "总体概览", Category = "核心分析", IsCore = true,
            Description = "打开目标并给出二进制总体概览（binary_overview）。",
            Parameters = new[]
            {
                Path(), Provider(), Snapshot(),
                new(){ Name="target-format", Label="格式提示 (可选)", Type=ArgType.Enum, Options=new[]{"dos-com","raw-binary"}, Hint="无头文件的显式解释"},
                new(){ Name="architecture", Label="架构 (可选)", Type=ArgType.Text, Hint="raw-binary 时必填的 CPU 架构"},
                new(){ Name="entry-point", Label="入口地址 (可选)", Type=ArgType.Text, Hint="raw-binary 时必填的 0xhex 入口"},
            },
        },
        new()
        {
            Name = "inspect", Title = "检查概览", Category = "核心分析", IsCore = true,
            Description = "以证据视角检查目标概览（binary_overview）。",
            Parameters = new[] { Path(), Provider(), Snapshot() },
        },
        new()
        {
            Name = "decompile", Title = "反编译", Category = "核心分析", IsCore = true,
            Description = "反编译指定函数为伪代码（procedure_pseudo_code）。",
            Parameters = new[]
            {
                Path(),
                Selector("procedure", "函数名 / 地址", 1, "procedure", "第二个位置参数或 --procedure= 传值"),
                new(){ Name="file-offset", Label="文件偏移 (可选)", Type=ArgType.Text, Hint="0xHEX，Ghidra 源字节偏移"},
                Provider(), Snapshot(),
            },
        },
        new()
        {
            Name = "function", Title = "函数分析", Category = "核心分析", IsCore = true,
            Description = "分析单个函数（analyze_function）。",
            Parameters = new[] { Path(), Selector("procedure", "函数名 / 地址", 1, "procedure", ""), Provider(), Snapshot() },
        },
        new()
        {
            Name = "instructions", Title = "函数指令", Category = "核心分析", IsCore = true,
            Description = "读取函数的原始反汇编指令（read_function_instructions）。",
            Parameters = new[] { Path(), Selector("procedure", "函数名 / 地址", 1, "procedure", ""), Provider(), Snapshot() },
        },
        new()
        {
            Name = "xrefs", Title = "交叉引用", Category = "核心分析", IsCore = true,
            Description = "查询地址/符号的交叉引用（xrefs）。",
            Parameters = new[] { Path(), Selector("address", "地址 / 符号", 1, "address", ""), Provider(), Snapshot() },
        },
        new()
        {
            Name = "search", Title = "搜索字符串/函数", Category = "核心分析", IsCore = true,
            Description = "在目标中搜索字符串或函数名（search_strings / search_procedures）。",
            Parameters = new[]
            {
                Path(),
                Selector("pattern", "搜索词", 1, "pattern", "第二个位置参数或 --pattern= 传值"),
                new(){ Name="kind", Label="类型", Type=ArgType.Enum, Options=new[]{"strings","procedures"}, Default="strings"},
                new(){ Name="mode", Label="匹配模式", Type=ArgType.Enum, Options=new[]{"literal","regex"}, Default="literal"},
                new(){ Name="case-sensitive", Label="区分大小写", Type=ArgType.Bool },
                Provider(), Snapshot(),
            },
        },
        new()
        {
            Name = "trace", Title = "特性追踪", Category = "核心分析", IsCore = true,
            Description = "按特性查询追踪相关代码路径（trace_feature）。",
            Parameters = new[]
            {
                Path(),
                Selector("query", "追踪查询", 1, "query", ""),
                new(){ Name="case-sensitive", Label="区分大小写", Type=ArgType.Bool },
                Provider(), Snapshot(),
            },
        },

        // ============ 字节 / 内存 ============
        new()
        {
            Name = "search-bytes", Title = "字节模式搜索", Category = "字节 / 内存",
            Description = "按字节模式（支持通配 mask）搜索目标。",
            Parameters = new[]
            {
                Path(),
                Selector("pattern", "字节模式", 1, "pattern", "如 48 8B ?? C3"),
                new(){ Name="limit", Label="返回上限", Type=ArgType.Int, Default="100", Hint="1..1000"},
                new(){ Name="start", Label="起始地址 (可选)", Type=ArgType.Text },
                new(){ Name="end", Label="结束地址 (可选)", Type=ArgType.Text },
                new(){ Name="mask", Label="掩码 (可选)", Type=ArgType.Text },
            },
        },
        new()
        {
            Name = "read-bytes", Title = "读取字节", Category = "字节 / 内存",
            Description = "读取指定地址处的一段字节。",
            Parameters = new[]
            {
                Path(),
                Selector("address", "地址", 1, "address", "0xhex"),
                new(){ Name="length", Label="长度", Type=ArgType.Int, Default="256", Hint="≥1"},
            },
        },
        new()
        {
            Name = "inspect-binary-layout", Title = "二进制布局", Category = "字节 / 内存",
            Description = "检查可执行文件的段/节布局。",
            Parameters = new[] { Path() },
        },
        new()
        {
            Name = "inspect-pe-resources", Title = "PE 资源", Category = "字节 / 内存",
            Description = "检查 PE 资源目录。",
            Parameters = new[] { Path() },
        },

        // ============ 环境与状态 ============
        new()
        {
            Name = "providers", Title = "已配置引擎", Category = "环境与状态",
            Description = "列出已配置的分析引擎（无参数）。",
        },
        new()
        {
            Name = "capabilities", Title = "引擎能力", Category = "环境与状态",
            Description = "列出各引擎能力与副作用（无参数）。",
        },
        new()
        {
            Name = "doctor", Title = "就绪诊断", Category = "环境与状态",
            Description = "检查 REA 是否就绪（引擎/依赖）。",
            Parameters = new CommandParameter[]
            {
                new(){ Name="target", Label="目标 (可选)", Type=ArgType.FilePath },
                new(){ Name="provider", Label="引擎 (可选)", Type=ArgType.Enum, Options=DoctorProviders },
                new(){ Name="skill", Label="含技能检查", Type=ArgType.Bool },
            },
        },
        new()
        {
            Name = "setup", Title = "环境配置", Category = "环境与状态",
            Description = "配置集成与可选引擎。默认建议先跑 --dry-run 预览计划。",
            Parameters = new CommandParameter[]
            {
                new(){ Name="dry-run", Label="仅预览 (dry-run)", Type=ArgType.Bool, Default="true", Hint="只打印计划，不写入" },
                new(){ Name="yes", Label="自动确认 (--yes)", Type=ArgType.Bool },
                new(){ Name="install-hopper", Label="安装 Hopper", Type=ArgType.Bool },
            },
        },
    };

    public static IEnumerable<string> Categories => All.Select(c => c.Category).Distinct();

    public static IEnumerable<ReaCommand> InCategory(string category) => All.Where(c => c.Category == category);

    public static IReadOnlyList<ReaCommand> Core => All.Where(c => c.IsCore).ToList();

    public static ReaCommand? Find(string name) => All.FirstOrDefault(c => c.Name == name);
}
