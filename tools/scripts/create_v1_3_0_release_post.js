const fs = require("fs");
const path = require("path");
const {
    AlignmentType,
    Document,
    Footer,
    HeadingLevel,
    ImageRun,
    LevelFormat,
    Packer,
    PageNumber,
    Paragraph,
    TextRun,
} = require("docx");

const root = path.resolve(__dirname, "../..");
const releaseDir = path.join(root, "docs", "archive", "release-posts");
const output = path.join(releaseDir, "XIAOFU工具箱_v1.3.0_公众号发布稿.docx");

const image = (name, width, height, description) => new Paragraph({
    alignment: AlignmentType.CENTER,
    spacing: { before: 160, after: 260 },
    children: [new ImageRun({
        type: "png",
        data: fs.readFileSync(path.join(releaseDir, name)),
        transformation: { width, height },
        altText: { title: description, description, name },
    })],
});

const heading = (text) => new Paragraph({
    heading: HeadingLevel.HEADING_1,
    children: [new TextRun(text)],
});

const body = (text, options = {}) => new Paragraph({
    alignment: options.alignment || AlignmentType.LEFT,
    spacing: { after: 150, line: 420 },
    children: [new TextRun({ text, ...options.run })],
});

const item = (text, boldPrefix) => new Paragraph({
    numbering: { reference: "bullets", level: 0 },
    spacing: { after: 90, line: 380 },
    children: boldPrefix
        ? [new TextRun({ text: boldPrefix, bold: true }), new TextRun(text)]
        : [new TextRun(text)],
});

const doc = new Document({
    styles: {
        default: { document: { run: { font: "Microsoft YaHei", size: 23, color: "1E2D3D" } } },
        paragraphStyles: [
            {
                id: "Title",
                name: "Title",
                basedOn: "Normal",
                next: "Normal",
                quickFormat: true,
                run: { font: "Microsoft YaHei", size: 42, bold: true, color: "0C3A5A" },
                paragraph: { alignment: AlignmentType.CENTER, spacing: { before: 120, after: 130 } },
            },
            {
                id: "Heading1",
                name: "Heading 1",
                basedOn: "Normal",
                next: "Normal",
                quickFormat: true,
                run: { font: "Microsoft YaHei", size: 31, bold: true, color: "087A8D" },
                paragraph: { spacing: { before: 360, after: 140 }, outlineLevel: 0 },
            },
        ],
    },
    numbering: {
        config: [{
            reference: "bullets",
            levels: [{
                level: 0,
                format: LevelFormat.BULLET,
                text: "•",
                alignment: AlignmentType.LEFT,
                style: { paragraph: { indent: { left: 540, hanging: 300 } } },
            }],
        }],
    },
    sections: [{
        properties: {
            page: {
                size: { width: 11906, height: 16838 },
                margin: { top: 1180, right: 1140, bottom: 1080, left: 1140 },
            },
        },
        footers: {
            default: new Footer({ children: [new Paragraph({
                alignment: AlignmentType.CENTER,
                children: [new TextRun({ text: "XIAOFU工具箱 v1.3.0  |  ", color: "6D7A86", size: 18 }), new TextRun({ children: [PageNumber.CURRENT], color: "6D7A86", size: 18 })],
            })] }),
        },
        children: [
            new Paragraph({
                heading: HeadingLevel.TITLE,
                children: [new TextRun("XIAOFU工具箱 v1.3.0 更新发布")],
            }),
            body("两个工具箱合一，MDB 转换与运行体验全面优化", {
                alignment: AlignmentType.CENTER,
                run: { color: "547083", size: 25 },
            }),
            body("2026 年 7 月 18 日", { alignment: AlignmentType.CENTER, run: { color: "71808A", size: 20 } }),
            image("XIAOFU工具箱_v1.3.0_公众号封面.png", 590, 354, "XIAOFU工具箱 v1.3.0 封面"),
            body("这次更新，XIAOFU工具箱完成了一项重要整合：原来需要分别安装的 XIAOFU工具箱 和 GIS Toolbox，现在合并为同一个 ArcGIS Pro 插件。升级后，只需安装一个 XIAOFUTools.esriAddinX，即可继续使用两套工具能力。"),

            heading("两个工具箱，现在安装一个就够了"),
            body("原 XIAOFU工具箱保留通用、编辑、数据、制图、系统等常用工具入口；原 GIS Toolbox 则以独立的 GIS 工具口袋标签页整合到同一个插件中。"),
            image("XIAOFU工具箱_v1.3.0_工具箱合并.png", 590, 354, "工具箱合并示意"),
            item("继续使用已有的数据处理、分析检查、制图输出和系统工具。", "XIAOFU工具箱："),
            item("集中收纳常用工具箱、内置命令、地图工具和自定义工具组。", "GIS 工具口袋："),
            body("GIS 工具口袋保留工具箱结构树、搜索、排序、隐藏、动态 Ribbon 入口，以及完整工具包导入导出等能力。首次未发现新配置时，也会兼容读取原 GIS Toolbox 配置。"),
            body("如果当前不需要此标签页，可进入“XIAOFU工具箱 → 系统 → 设置”，通过“显示 GIS 工具口袋标签页”随时打开或关闭。"),

            heading("MDB批量格式转换：三种输出格式，更直接"),
            body("MDB批量格式转换已升级为调用 ArcGIS Pro 3.7 自带的 Convert Personal Geodatabase 转换工具，不再使用旧的自制转换链路。"),
            image("XIAOFU工具箱_v1.3.0_MDB格式转换.png", 590, 393, "MDB批量格式转换输出格式"),
            item(".gdb", "文件地理数据库："),
            item(".geodatabase", "移动地理数据库："),
            item(".xml", "XML Workspace Document："),
            body("工具仍保留批量扫描、选择转换项、输出位置规划、覆盖控制、停止取消和必要操作日志。转换过程直接使用 ArcGIS Pro 的原生能力，输出类型与软件内置工具保持一致。"),

            heading("常用操作更流畅"),
            image("XIAOFU工具箱_v1.3.0_运行体验优化.png", 590, 393, "运行体验优化"),
            item("耗时的地理处理任务尽量在后台执行，减少处理过程中对界面操作的影响。"),
            item("常用数据扫描、数据库刷新等操作减少同步等待，打开和刷新更轻快。"),
            item("图幅与数据库相关批量任务完善资源释放，降低操作完成后数据仍被占用的情况。"),
            item("正式工具移除开发期自检和临时数据创建流程，日志只保留转换、处理、警告、错误和完成等关键信息。"),

            heading("本次同步更新"),
            item("驱动制图增加交集表格设置、面积调平、布局表格生成和批量导出联动。"),
            item("优化极小“其他”交集面积处理，避免产生无意义表格行。"),
            item("修复 KML/KMZ 标注字段下拉框鼠标展开及字段刷新后选择被重置的问题。"),
            item("AI 助手不再保留内置默认 API、密钥和模型；升级时会清理旧内置配置，同时保留用户自定义模型。"),

            heading("如何升级"),
            body("下载并安装最新版 XIAOFUTools.esriAddinX，安装后重启 ArcGIS Pro 即可。若电脑上此前同时安装旧版 XIAOFU工具箱和 GIS Toolbox，升级到 v1.3.0 后以新版 XIAOFUTools 为准，不再需要额外安装 GIS Toolbox。"),
            body("仅支持 ArcGIS Pro 3.7。", { run: { bold: true, color: "087A8D" } }),
            heading("获取方式"),
            body("关注公众号，回复「XIAOFU工具箱」获取下载链接。", { alignment: AlignmentType.CENTER, run: { bold: true, color: "0C3A5A", size: 25 } }),
            body("XIAOFU工具箱 v1.3.0：一个插件整合两套工具能力，让常用 GIS 工具更集中，MDB 数据迁移更直接，日常操作也更流畅。", { alignment: AlignmentType.CENTER, run: { bold: true, color: "0C3A5A", size: 24 } }),
        ],
    }],
});

Packer.toBuffer(doc).then((buffer) => {
    fs.writeFileSync(output, buffer);
    process.stdout.write(`${output}\n`);
});
