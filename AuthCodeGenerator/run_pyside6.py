#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
启动PySide6版本的授权码生成器
"""

import sys
import subprocess
import os

def check_and_install_dependencies():
    """检查并安装依赖"""
    try:
        import PySide6
        print("PySide6 已安装")
    except ImportError:
        print("正在安装 PySide6...")
        try:
            subprocess.check_call([sys.executable, "-m", "pip", "install", "PySide6"])
            print("PySide6 安装成功")
        except subprocess.CalledProcessError:
            print("PySide6 安装失败，请手动安装：pip install PySide6")
            return False
    return True

def main():
    print("XIAOFU工具箱授权码生成器 - PySide6版本启动器")
    print("=" * 50)
    
    # 检查依赖
    if not check_and_install_dependencies():
        input("按回车键退出...")
        return
    
    # 启动应用
    try:
        from auth_code_generator_pyside6 import main as app_main
        print("正在启动应用...")
        app_main()
    except Exception as e:
        print(f"启动失败: {e}")
        input("按回车键退出...")

if __name__ == "__main__":
    main()
