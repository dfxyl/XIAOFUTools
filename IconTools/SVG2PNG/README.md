# SVG转PNG工具

简单易用的SVG转PNG转换工具，支持GUI和命令行两种模式，一键输出16x16和32x32尺寸图标。

## 快速开始

### GUI模式
双击 `SVG2PNG.exe` → 拖拽SVG文件 → 选择输出目录 → 点击转换

### 命令行模式
```bash
# 转换单个文件
SVG2PNG.exe icon.svg

# 批量转换文件夹
SVG2PNG.exe -o output icons/

# 查看帮助
SVG2PNG.exe --help
```

## 命令行选项

| 选项 | 说明 |
|------|------|
| `-h, --help` | 显示帮助 |
| `-o, --output` | 指定输出目录 |
| `-q, --quiet` | 静默模式 |
| `-v, --verbose` | 详细模式 |

## 输出说明

每个SVG文件生成：
- `文件名_16.png` (16x16像素)
- `文件名_32.png` (32x32像素)

## 系统要求

- Windows 10/11
- .NET 8.0 Runtime

## 注意事项

- 确保SVG文件格式正确
- 输出目录需要写入权限
- 已存在文件会被覆盖
