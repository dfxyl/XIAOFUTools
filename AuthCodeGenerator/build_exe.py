#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Nuitka打包脚本 - 授权码生成器极简版
"""

import os
import sys
import subprocess
import shutil
from pathlib import Path

def clean_build_dirs():
    """清理构建目录"""
    dirs_to_clean = [
        "auth_code_generator_pyside6.build",
        "auth_code_generator_pyside6.dist",
        "auth_code_generator_pyside6.onefile-build"
    ]
    
    for dir_name in dirs_to_clean:
        if os.path.exists(dir_name):
            print(f"清理目录: {dir_name}")
            shutil.rmtree(dir_name)

def build_exe():
    """使用Nuitka构建exe"""
    print("=" * 60)
    print("    授权码生成器 - Nuitka打包工具")
    print("=" * 60)
    
    # 清理旧的构建文件
    clean_build_dirs()
    
    # Nuitka命令参数
    nuitka_args = [
        "python", "-m", "nuitka",
        
        # 基本设置
        "--standalone",                    # 独立模式
        "--onefile",                      # 单文件模式
        "--output-filename=授权码生成器.exe", # 输出文件名
        
        # 优化设置
        "--assume-yes-for-downloads",     # 自动下载依赖
        "--remove-output",               # 移除输出目录
        "--no-pyi-file",                # 不生成pyi文件
        
        # PySide6相关设置
        "--enable-plugin=pyside6",       # 启用PySide6插件
        "--include-qt-plugins=sensible", # 包含必要的Qt插件
        
        # 性能优化
        "--lto=no",                      # 禁用链接时优化(加快编译)
        "--jobs=4",                      # 并行编译
        
        # 图标设置（如果有图标文件）
        # "--windows-icon-from-ico=icon.ico",
        
        # 控制台设置
        "--windows-console-mode=disable", # 禁用控制台窗口
        
        # 包含数据文件
        "--include-data-files=settings.json=settings.json",  # 如果存在设置文件
        
        # 目标文件
        "auth_code_generator_pyside6.py"
    ]
    
    print("开始构建...")
    print("命令:", " ".join(nuitka_args))
    print()
    
    try:
        # 执行Nuitka命令
        result = subprocess.run(nuitka_args, check=True, capture_output=False)
        
        print()
        print("=" * 60)
        print("✅ 构建成功!")
        
        # 检查输出文件
        exe_file = "授权码生成器.exe"
        if os.path.exists(exe_file):
            file_size = os.path.getsize(exe_file) / (1024 * 1024)  # MB
            print(f"📦 输出文件: {exe_file}")
            print(f"📏 文件大小: {file_size:.1f} MB")
            print(f"📍 文件路径: {os.path.abspath(exe_file)}")
        else:
            print("⚠️  未找到输出文件")
        
        print("=" * 60)
        
    except subprocess.CalledProcessError as e:
        print(f"❌ 构建失败: {e}")
        return False
    except Exception as e:
        print(f"❌ 发生错误: {e}")
        return False
    
    return True

def build_simple():
    """简化版构建（更快）"""
    print("=" * 60)
    print("    授权码生成器 - 快速构建模式")
    print("=" * 60)
    
    clean_build_dirs()
    
    # 简化的Nuitka参数
    nuitka_args = [
        "python", "-m", "nuitka",
        "--standalone",
        "--output-filename=授权码生成器_简化版.exe",
        "--assume-yes-for-downloads",
        "--remove-output",
        "--enable-plugin=pyside6",
        "--windows-console-mode=disable",
        "auth_code_generator_pyside6.py"
    ]
    
    print("开始快速构建...")
    print("命令:", " ".join(nuitka_args))
    print()
    
    try:
        subprocess.run(nuitka_args, check=True)
        print("✅ 快速构建完成!")
        
        exe_file = "授权码生成器_简化版.exe"
        if os.path.exists(exe_file):
            file_size = os.path.getsize(exe_file) / (1024 * 1024)
            print(f"📦 输出文件: {exe_file} ({file_size:.1f} MB)")
        
    except Exception as e:
        print(f"❌ 构建失败: {e}")
        return False
    
    return True

def main():
    """主函数"""
    if not os.path.exists("auth_code_generator_pyside6.py"):
        print("❌ 未找到源文件: auth_code_generator_pyside6.py")
        return
    
    print("请选择构建模式:")
    print("1. 完整构建 (单文件，体积较大，启动较快)")
    print("2. 快速构建 (多文件，体积较小，构建较快)")
    print("3. 退出")
    
    while True:
        choice = input("\n请输入选择 (1-3): ").strip()
        
        if choice == "1":
            build_exe()
            break
        elif choice == "2":
            build_simple()
            break
        elif choice == "3":
            print("退出构建")
            break
        else:
            print("无效选择，请重新输入")

if __name__ == "__main__":
    main()
