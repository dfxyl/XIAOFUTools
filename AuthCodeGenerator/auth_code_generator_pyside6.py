#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
XIAOFU工具箱授权码生成器 v2.0 - PySide6版本
极简风格设计
作者: XIAOFU
QQ: 1922759464
Q群: 967758553
"""

import sys
import hashlib
import base64
from datetime import datetime, timedelta
import json
import os
from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                               QHBoxLayout, QGridLayout, QLabel, QPushButton, 
                               QTextEdit, QSpinBox, QRadioButton, QButtonGroup,
                               QLineEdit, QMessageBox, QFileDialog, QFrame,
                               QScrollArea, QSizePolicy)
from PySide6.QtCore import Qt, QTimer
from PySide6.QtGui import QFont, QClipboard, QPalette, QColor

class ModernButton(QPushButton):
    """极简按钮样式"""
    def __init__(self, text, primary=False, small=False):
        super().__init__(text)
        self.primary = primary
        self.small = small
        height = 26 if small else 32
        self.setFixedHeight(height)
        self.setMinimumWidth(60 if small else 80)
        self.setCursor(Qt.PointingHandCursor)
        self.apply_style()

    def apply_style(self):
        if self.primary:
            self.setStyleSheet("""
                QPushButton {
                    background: qlineargradient(x1:0, y1:0, x2:0, y2:1,
                        stop:0 #0d6efd, stop:1 #0b5ed7);
                    color: white;
                    border: none;
                    border-radius: 4px;
                    font-weight: 500;
                    font-size: 12px;
                }
                QPushButton:hover {
                    background: qlineargradient(x1:0, y1:0, x2:0, y2:1,
                        stop:0 #0b5ed7, stop:1 #0a58ca);
                }
                QPushButton:pressed {
                    background: #0a58ca;
                }
            """)
        else:
            self.setStyleSheet("""
                QPushButton {
                    background: white;
                    color: #495057;
                    border: 1px solid #ced4da;
                    border-radius: 4px;
                    font-size: 11px;
                }
                QPushButton:hover {
                    background: #f8f9fa;
                    border-color: #adb5bd;
                }
                QPushButton:pressed {
                    background: #e9ecef;
                }
            """)

class ModernTextEdit(QTextEdit):
    """极简文本编辑框"""
    def __init__(self, height=60):
        super().__init__()
        self.setFixedHeight(height)
        self.setStyleSheet("""
            QTextEdit {
                border: 1px solid #ced4da;
                border-radius: 4px;
                padding: 8px;
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
                font-size: 12px;
                color: #212529;
                background-color: white;
                selection-background-color: #0d6efd;
                selection-color: white;
            }
            QTextEdit:focus {
                border-color: #0d6efd;
                outline: none;
                background-color: #ffffff;
            }
            QTextEdit:read-only {
                background-color: #f8f9fa;
                color: #495057;
            }
        """)

class ModernLineEdit(QLineEdit):
    """极简单行输入框"""
    def __init__(self):
        super().__init__()
        self.setFixedHeight(28)
        self.setStyleSheet("""
            QLineEdit {
                border: 1px solid #ced4da;
                border-radius: 4px;
                padding: 0 8px;
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
                font-size: 12px;
                color: #212529;
                background-color: white;
            }
            QLineEdit:focus {
                border-color: #0d6efd;
                outline: none;
                background-color: #ffffff;
            }
        """)

class ModernSpinBox(QSpinBox):
    """极简数字输入框"""
    def __init__(self):
        super().__init__()
        self.setFixedHeight(28)
        self.setStyleSheet("""
            QSpinBox {
                border: 1px solid #ced4da;
                border-radius: 4px;
                padding: 0 6px;
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
                font-size: 12px;
                color: #212529;
                background-color: white;
            }
            QSpinBox:focus {
                border-color: #0d6efd;
                outline: none;
                background-color: #ffffff;
            }
            QSpinBox::up-button, QSpinBox::down-button {
                width: 16px;
                border: none;
                background-color: #f8f9fa;
            }
            QSpinBox::up-button:hover, QSpinBox::down-button:hover {
                background-color: #e9ecef;
            }
        """)

class AuthCodeGenerator(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("授权码生成器")
        self.setFixedSize(420, 480)
        self.setStyleSheet("""
            QMainWindow {
                background: qlineargradient(x1:0, y1:0, x2:0, y2:1,
                    stop:0 #f8f9fa, stop:1 #e9ecef);
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
            }
            QLabel {
                color: #212529;
                font-size: 12px;
                font-weight: 500;
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
            }
            QRadioButton {
                color: #212529;
                font-size: 12px;
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
                spacing: 6px;
            }
            QRadioButton::indicator {
                width: 14px;
                height: 14px;
            }
            QRadioButton::indicator:unchecked {
                border: 2px solid #dee2e6;
                border-radius: 7px;
                background-color: white;
            }
            QRadioButton::indicator:checked {
                border: 2px solid #0d6efd;
                border-radius: 7px;
                background: qradialgradient(cx:0.5, cy:0.5, radius:0.5,
                    stop:0 #0d6efd, stop:0.7 #0d6efd, stop:1 white);
            }
        """)
        
        self.setup_ui()
        self.load_settings()
    
    def setup_ui(self):
        """设置极简用户界面"""
        central_widget = QWidget()
        self.setCentralWidget(central_widget)

        # 主布局
        main_layout = QVBoxLayout(central_widget)
        main_layout.setContentsMargins(16, 16, 16, 16)
        main_layout.setSpacing(12)

        # 标题区域 - 极简化
        title_label = QLabel("授权码生成器")
        title_label.setAlignment(Qt.AlignCenter)
        title_label.setFont(QFont("Microsoft YaHei", 14, QFont.Bold))
        title_label.setStyleSheet("color: #212529; margin-bottom: 8px;")
        main_layout.addWidget(title_label)
        
        # 授权类型选择 - 紧凑布局
        type_layout = QHBoxLayout()
        type_layout.setSpacing(16)

        type_label = QLabel("类型:")
        type_label.setMinimumWidth(40)

        self.personal_radio = QRadioButton("个人版")
        self.universal_radio = QRadioButton("通用版")
        self.personal_radio.setChecked(True)

        self.auth_type_group = QButtonGroup()
        self.auth_type_group.addButton(self.personal_radio, 0)
        self.auth_type_group.addButton(self.universal_radio, 1)
        self.auth_type_group.buttonClicked.connect(self.on_type_changed)

        type_layout.addWidget(type_label)
        type_layout.addWidget(self.personal_radio)
        type_layout.addWidget(self.universal_radio)
        type_layout.addStretch()
        main_layout.addLayout(type_layout)
        
        # 机器码输入区域 - 紧凑布局
        self.machine_code_layout = QVBoxLayout()
        self.machine_code_layout.setSpacing(4)

        self.machine_code_label = QLabel("机器码:")

        self.machine_code_text = ModernTextEdit(60)
        self.machine_code_text.setPlaceholderText("请输入机器码...")

        self.machine_code_layout.addWidget(self.machine_code_label)
        self.machine_code_layout.addWidget(self.machine_code_text)
        main_layout.addLayout(self.machine_code_layout)
        
        # 授权时长设置 - 紧凑布局
        duration_layout = QHBoxLayout()
        duration_layout.setSpacing(8)

        duration_label = QLabel("时长:")
        duration_label.setMinimumWidth(40)

        self.duration_spinbox = ModernSpinBox()
        self.duration_spinbox.setRange(1, 3650)
        self.duration_spinbox.setValue(30)
        self.duration_spinbox.setFixedWidth(60)

        days_label = QLabel("天")

        # 快速选择按钮 - 小尺寸
        quick_buttons = [
            ("7", 7), ("30", 30), ("90", 90), ("365", 365)
        ]

        for text, days in quick_buttons:
            btn = ModernButton(text, small=True)
            btn.setFixedWidth(35)
            btn.clicked.connect(lambda checked, d=days: self.duration_spinbox.setValue(d))
            duration_layout.addWidget(btn)

        duration_layout.insertWidget(0, duration_label)
        duration_layout.insertWidget(1, self.duration_spinbox)
        duration_layout.insertWidget(2, days_label)
        duration_layout.addStretch()
        main_layout.addLayout(duration_layout)
        
        # 自定义过期时间 - 紧凑布局
        expire_layout = QHBoxLayout()
        expire_layout.setSpacing(8)

        expire_label = QLabel("过期:")
        expire_label.setMinimumWidth(40)

        self.expire_date_edit = ModernLineEdit()
        self.expire_date_edit.setPlaceholderText("YYYY-MM-DD HH:MM:SS")
        self.expire_date_edit.setFixedWidth(140)

        use_duration_btn = ModernButton("自动", small=True)
        use_duration_btn.setFixedWidth(40)
        use_duration_btn.clicked.connect(self.use_duration)

        expire_layout.addWidget(expire_label)
        expire_layout.addWidget(self.expire_date_edit)
        expire_layout.addWidget(use_duration_btn)
        expire_layout.addStretch()
        main_layout.addLayout(expire_layout)

        # 生成按钮
        generate_btn = ModernButton("生成授权码", primary=True)
        generate_btn.setFixedHeight(36)
        generate_btn.clicked.connect(self.generate_auth_code)
        main_layout.addWidget(generate_btn)
        
        # 授权码显示区域 - 紧凑布局
        result_layout = QVBoxLayout()
        result_layout.setSpacing(4)

        result_label = QLabel("授权码:")

        self.auth_code_text = ModernTextEdit(80)
        self.auth_code_text.setReadOnly(True)
        self.auth_code_text.setPlaceholderText("授权码将在这里显示...")

        result_layout.addWidget(result_label)
        result_layout.addWidget(self.auth_code_text)
        main_layout.addLayout(result_layout)

        # 操作按钮 - 紧凑布局
        button_layout = QHBoxLayout()
        button_layout.setSpacing(8)

        copy_btn = ModernButton("复制", small=True)
        save_btn = ModernButton("保存", small=True)
        clear_btn = ModernButton("清空", small=True)

        copy_btn.clicked.connect(self.copy_auth_code)
        save_btn.clicked.connect(self.save_to_file)
        clear_btn.clicked.connect(self.clear_all)

        button_layout.addWidget(copy_btn)
        button_layout.addWidget(save_btn)
        button_layout.addWidget(clear_btn)
        button_layout.addStretch()

        main_layout.addLayout(button_layout)

        # 状态栏 - 极简
        self.status_label = QLabel("就绪")
        self.status_label.setStyleSheet("""
            QLabel {
                color: #495057;
                font-size: 11px;
                font-family: 'Microsoft YaHei', 'SimHei', sans-serif;
                padding: 4px 8px;
                background-color: #f8f9fa;
                border-radius: 3px;
                border: 1px solid #dee2e6;
            }
        """)
        main_layout.addWidget(self.status_label)
        
        # 初始化界面状态
        self.on_type_changed()

    def on_type_changed(self):
        """授权类型改变时的处理"""
        if self.universal_radio.isChecked():
            # 通用版不需要机器码
            self.machine_code_label.hide()
            self.machine_code_text.hide()
            self.status_label.setText("通用版模式：生成的授权码可在任何电脑上使用")
        else:
            # 个人版需要机器码
            self.machine_code_label.show()
            self.machine_code_text.show()
            self.status_label.setText("个人版模式：需要输入用户的机器码")

    def use_duration(self):
        """使用当前时间加上指定天数"""
        try:
            days = self.duration_spinbox.value()
            expire_time = datetime.now() + timedelta(days=days)
            self.expire_date_edit.setText(expire_time.strftime("%Y-%m-%d %H:%M:%S"))
        except Exception as e:
            QMessageBox.critical(self, "错误", f"设置过期时间失败: {str(e)}")

    def generate_auth_code(self):
        """生成授权码"""
        try:
            auth_type = "通用版" if self.universal_radio.isChecked() else "个人版"

            # 获取过期时间
            expire_time_str = self.expire_date_edit.text().strip()
            if expire_time_str:
                try:
                    expire_time = datetime.strptime(expire_time_str, "%Y-%m-%d %H:%M:%S")
                except ValueError:
                    QMessageBox.critical(self, "错误", "过期时间格式不正确，请使用 YYYY-MM-DD HH:MM:SS 格式")
                    return
            else:
                # 使用天数计算过期时间
                days = self.duration_spinbox.value()
                expire_time = datetime.now() + timedelta(days=days)

            # 检查过期时间是否在未来
            if expire_time <= datetime.now():
                QMessageBox.critical(self, "错误", "过期时间必须在当前时间之后")
                return

            # 根据类型生成授权码
            if auth_type == "通用版":
                auth_code = self.create_universal_auth_code(expire_time)
            else:
                # 个人版需要机器码
                machine_code = self.machine_code_text.toPlainText().strip()
                if not machine_code:
                    QMessageBox.critical(self, "错误", "请输入机器码")
                    return
                auth_code = self.create_personal_auth_code(machine_code, expire_time)

            # 显示授权码
            self.auth_code_text.setPlainText(auth_code)

            # 更新状态
            type_desc = f"（{auth_type}）"
            self.status_label.setText(f"授权码生成成功{type_desc}，过期时间: {expire_time.strftime('%Y-%m-%d %H:%M:%S')}")

        except Exception as e:
            QMessageBox.critical(self, "错误", f"生成授权码失败: {str(e)}")
            self.status_label.setText("生成失败")

    def generate_complex_hash(self, input_str):
        """生成复杂哈希"""
        # 多重哈希
        hash1 = hashlib.sha256(input_str.encode('utf-8')).digest()
        hash1_b64 = base64.b64encode(hash1).decode('utf-8')

        hash2_input = hash1_b64 + "COMPLEX_SALT_2024"
        hash2 = hashlib.sha256(hash2_input.encode('utf-8')).digest()
        hash2_b64 = base64.b64encode(hash2).decode('utf-8')

        hash3_input = hash2_b64 + str(len(input_str))
        hash3 = hashlib.sha256(hash3_input.encode('utf-8')).digest()
        hash3_b64 = base64.b64encode(hash3).decode('utf-8')

        return hash1_b64 + hash2_b64 + hash3_b64

    def create_personal_auth_code(self, machine_code, expire_time):
        """创建个人版授权码"""
        try:
            # 生成复杂机器码哈希（取前32位）
            machine_hash_full = self.generate_complex_hash(machine_code)
            machine_hash = machine_hash_full[:32] if len(machine_hash_full) >= 32 else machine_hash_full

            # 转换过期时间为.NET DateTime.ToBinary()兼容格式
            epoch_start = datetime(1, 1, 1)
            ticks = int((expire_time - epoch_start).total_seconds() * 10000000)  # 100纳秒刻度
            timestamp_str = str(ticks)

            # 生成校验码（取前16位）
            checksum_input = machine_hash + timestamp_str + "XIAOFU_CHECK"
            checksum_full = self.generate_complex_hash(checksum_input)
            checksum = checksum_full[:16] if len(checksum_full) >= 16 else checksum_full

            # 生成签名（取前20位）
            signature_input = machine_hash + timestamp_str + checksum + "XIAOFU_SIGN"
            signature_full = self.generate_complex_hash(signature_input)
            signature = signature_full[:20] if len(signature_full) >= 20 else signature_full

            # 组合授权码数据
            auth_data = f"{machine_hash}|{timestamp_str}|{checksum}|{signature}"

            # 复杂加密
            encrypted_auth_code = self.encrypt_complex_auth_code(auth_data)

            return encrypted_auth_code

        except Exception as e:
            raise Exception(f"创建个人版授权码失败: {str(e)}")

    def create_universal_auth_code(self, expire_time):
        """创建通用版授权码"""
        try:
            # 转换过期时间为.NET DateTime.ToBinary()兼容格式
            epoch_start = datetime(1, 1, 1)
            ticks = int((expire_time - epoch_start).total_seconds() * 10000000)  # 100纳秒刻度
            timestamp_str = str(ticks)

            # 生成校验码（取前16位）
            checksum_input = "UNIVERSAL" + timestamp_str + "XIAOFU_UNIVERSAL_CHECK"
            checksum_full = self.generate_complex_hash(checksum_input)
            checksum = checksum_full[:16] if len(checksum_full) >= 16 else checksum_full

            # 生成签名（取前20位）
            signature_input = "UNIVERSAL" + timestamp_str + checksum + "XIAOFU_UNIVERSAL_SIGN"
            signature_full = self.generate_complex_hash(signature_input)
            signature = signature_full[:20] if len(signature_full) >= 20 else signature_full

            # 组合授权码数据
            auth_data = f"UNIVERSAL|{timestamp_str}|{checksum}|{signature}"

            # 复杂加密
            encrypted_auth_code = self.encrypt_complex_auth_code(auth_data)

            # 添加通用版前缀
            return "UNIVERSAL_" + encrypted_auth_code

        except Exception as e:
            raise Exception(f"创建通用版授权码失败: {str(e)}")

    def encrypt_complex_auth_code(self, auth_data):
        """复杂加密授权码"""
        try:
            # XOR加密
            key = "XIAOFU_COMPLEX_KEY_2024_SECURE".encode('utf-8')
            auth_bytes = auth_data.encode('utf-8')

            encrypted = bytearray()
            for i, byte in enumerate(auth_bytes):
                encrypted.append(byte ^ key[i % len(key)])

            # 第一层Base64编码
            layer1 = base64.b64encode(encrypted).decode('utf-8')

            # 第二层Base64编码
            layer2 = base64.b64encode(layer1.encode('utf-8')).decode('utf-8')

            return layer2

        except Exception as e:
            raise Exception(f"加密授权码失败: {str(e)}")

    def copy_auth_code(self):
        """复制授权码到剪贴板"""
        try:
            auth_code = self.auth_code_text.toPlainText().strip()
            if not auth_code:
                QMessageBox.warning(self, "警告", "没有授权码可复制")
                return

            clipboard = QApplication.clipboard()
            clipboard.setText(auth_code)
            self.status_label.setText("授权码已复制到剪贴板")
            QMessageBox.information(self, "成功", "授权码已复制到剪贴板")

        except Exception as e:
            QMessageBox.critical(self, "错误", f"复制失败: {str(e)}")

    def save_to_file(self):
        """保存授权码到文件"""
        try:
            auth_code = self.auth_code_text.toPlainText().strip()
            if not auth_code:
                QMessageBox.warning(self, "警告", "没有授权码可保存")
                return

            filename, _ = QFileDialog.getSaveFileName(
                self,
                "保存授权码",
                "授权码.txt",
                "文本文件 (*.txt);;所有文件 (*.*)"
            )

            if filename:
                auth_type = "通用版" if self.universal_radio.isChecked() else "个人版"
                with open(filename, 'w', encoding='utf-8') as f:
                    f.write(f"XIAOFU工具箱授权码\n")
                    f.write(f"生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
                    f.write(f"授权类型: {auth_type}\n")
                    f.write(f"授权码:\n{auth_code}\n")

                self.status_label.setText(f"授权码已保存到: {filename}")
                QMessageBox.information(self, "成功", f"授权码已保存到:\n{filename}")

        except Exception as e:
            QMessageBox.critical(self, "错误", f"保存失败: {str(e)}")

    def clear_all(self):
        """清空所有内容"""
        self.machine_code_text.clear()
        self.auth_code_text.clear()
        self.expire_date_edit.clear()
        self.duration_spinbox.setValue(30)
        self.personal_radio.setChecked(True)
        self.on_type_changed()
        self.status_label.setText("已清空")

    def load_settings(self):
        """加载设置"""
        try:
            if os.path.exists("settings.json"):
                with open("settings.json", 'r', encoding='utf-8') as f:
                    settings = json.load(f)
                    self.duration_spinbox.setValue(settings.get("default_duration", 30))
                    auth_type = settings.get("default_auth_type", "个人版")
                    if auth_type == "通用版":
                        self.universal_radio.setChecked(True)
                    else:
                        self.personal_radio.setChecked(True)
                    self.on_type_changed()
        except:
            pass

    def save_settings(self):
        """保存设置"""
        try:
            auth_type = "通用版" if self.universal_radio.isChecked() else "个人版"
            settings = {
                "default_duration": self.duration_spinbox.value(),
                "default_auth_type": auth_type
            }
            with open("settings.json", 'w', encoding='utf-8') as f:
                json.dump(settings, f, ensure_ascii=False, indent=2)
        except:
            pass

    def closeEvent(self, event):
        """窗口关闭事件"""
        self.save_settings()
        event.accept()

def main():
    app = QApplication(sys.argv)

    # 设置应用程序样式
    app.setStyle('Fusion')

    # 设置全局字体
    font = QFont("Microsoft YaHei", 9)
    app.setFont(font)

    window = AuthCodeGenerator()
    window.show()

    sys.exit(app.exec())

if __name__ == "__main__":
    main()
