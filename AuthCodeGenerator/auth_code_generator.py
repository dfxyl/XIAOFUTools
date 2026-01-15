#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
XIAOFU工具箱授权码生成器 v2.0
支持生成个人版（机器码绑定）和通用版（所有电脑可用）授权码
作者: XIAOFU
QQ: 1922759464
Q群: 967758553
"""

import tkinter as tk
from tkinter import ttk, messagebox, filedialog
import hashlib
import base64
from datetime import datetime, timedelta
import json
import os

class AuthCodeGenerator:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("XIAOFU工具箱授权码生成器 v2.0")
        self.root.geometry("650x600")
        self.root.resizable(True, True)
        
        # 设置窗口图标（如果有的话）
        try:
            self.root.iconbitmap("icon.ico")
        except:
            pass
        
        self.setup_ui()
        self.load_settings()
    
    def setup_ui(self):
        """设置用户界面"""
        # 主框架
        main_frame = ttk.Frame(self.root, padding="10")
        main_frame.grid(row=0, column=0, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        # 配置网格权重
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(0, weight=1)
        main_frame.columnconfigure(1, weight=1)
        
        # 标题
        title_label = ttk.Label(main_frame, text="XIAOFU工具箱授权码生成器 v2.0", 
                               font=("Arial", 16, "bold"))
        title_label.grid(row=0, column=0, columnspan=3, pady=(0, 10))
        
        # 作者信息
        author_label = ttk.Label(main_frame, text="作者: XIAOFU | QQ: 1922759464 | Q群: 967758553", 
                                font=("Arial", 10))
        author_label.grid(row=1, column=0, columnspan=3, pady=(0, 20))
        
        # 授权类型选择
        ttk.Label(main_frame, text="授权类型:").grid(row=2, column=0, sticky=tk.W, pady=5)
        self.auth_type_var = tk.StringVar(value="个人版")
        type_frame = ttk.Frame(main_frame)
        type_frame.grid(row=2, column=1, columnspan=2, sticky=(tk.W, tk.E), pady=5)
        
        ttk.Radiobutton(type_frame, text="个人版（绑定机器码）", variable=self.auth_type_var, 
                       value="个人版", command=self.on_type_changed).grid(row=0, column=0, padx=(0, 20))
        ttk.Radiobutton(type_frame, text="通用版（所有电脑可用）", variable=self.auth_type_var, 
                       value="通用版", command=self.on_type_changed).grid(row=0, column=1)
        
        # 机器码输入区域
        self.machine_code_label = ttk.Label(main_frame, text="机器码:")
        self.machine_code_label.grid(row=3, column=0, sticky=tk.W, pady=5)
        
        machine_code_frame = ttk.Frame(main_frame)
        machine_code_frame.grid(row=3, column=1, columnspan=2, sticky=(tk.W, tk.E), pady=5)
        machine_code_frame.columnconfigure(0, weight=1)
        
        self.machine_code_text = tk.Text(machine_code_frame, height=4, wrap=tk.WORD)
        self.machine_code_text.grid(row=0, column=0, sticky=(tk.W, tk.E))
        
        # 机器码滚动条
        machine_scrollbar = ttk.Scrollbar(machine_code_frame, orient=tk.VERTICAL, 
                                         command=self.machine_code_text.yview)
        machine_scrollbar.grid(row=0, column=1, sticky=(tk.N, tk.S))
        self.machine_code_text.configure(yscrollcommand=machine_scrollbar.set)
        
        # 授权时长设置
        ttk.Label(main_frame, text="授权时长:").grid(row=4, column=0, sticky=tk.W, pady=5)
        duration_frame = ttk.Frame(main_frame)
        duration_frame.grid(row=4, column=1, columnspan=2, sticky=(tk.W, tk.E), pady=5)
        
        self.duration_var = tk.IntVar(value=30)
        duration_spinbox = ttk.Spinbox(duration_frame, from_=1, to=3650, 
                                      textvariable=self.duration_var, width=10)
        duration_spinbox.grid(row=0, column=0, padx=(0, 5))
        ttk.Label(duration_frame, text="天").grid(row=0, column=1)
        
        # 快速选择按钮
        quick_frame = ttk.Frame(duration_frame)
        quick_frame.grid(row=0, column=2, padx=(20, 0))
        
        ttk.Button(quick_frame, text="7天", width=6,
                  command=lambda: self.duration_var.set(7)).grid(row=0, column=0, padx=2)
        ttk.Button(quick_frame, text="30天", width=6,
                  command=lambda: self.duration_var.set(30)).grid(row=0, column=1, padx=2)
        ttk.Button(quick_frame, text="90天", width=6,
                  command=lambda: self.duration_var.set(90)).grid(row=0, column=2, padx=2)
        ttk.Button(quick_frame, text="365天", width=6,
                  command=lambda: self.duration_var.set(365)).grid(row=0, column=3, padx=2)
        
        # 自定义过期时间
        ttk.Label(main_frame, text="或指定过期时间:").grid(row=5, column=0, sticky=tk.W, pady=5)
        expire_frame = ttk.Frame(main_frame)
        expire_frame.grid(row=5, column=1, columnspan=2, sticky=(tk.W, tk.E), pady=5)
        
        self.expire_date_var = tk.StringVar()
        expire_entry = ttk.Entry(expire_frame, textvariable=self.expire_date_var, width=20)
        expire_entry.grid(row=0, column=0, padx=(0, 5))
        ttk.Label(expire_frame, text="(格式: YYYY-MM-DD HH:MM:SS)").grid(row=0, column=1)
        
        ttk.Button(expire_frame, text="使用当前时间+天数", 
                  command=self.use_duration).grid(row=0, column=2, padx=(10, 0))
        
        # 生成按钮
        generate_btn = ttk.Button(main_frame, text="生成授权码", 
                                 command=self.generate_auth_code)
        generate_btn.grid(row=6, column=0, columnspan=3, pady=20)
        
        # 授权码显示区域
        ttk.Label(main_frame, text="生成的授权码:").grid(row=7, column=0, sticky=tk.W, pady=5)
        auth_code_frame = ttk.Frame(main_frame)
        auth_code_frame.grid(row=7, column=1, columnspan=2, sticky=(tk.W, tk.E), pady=5)
        auth_code_frame.columnconfigure(0, weight=1)
        
        self.auth_code_text = tk.Text(auth_code_frame, height=6, wrap=tk.WORD)
        self.auth_code_text.grid(row=0, column=0, sticky=(tk.W, tk.E))
        
        # 授权码滚动条
        auth_scrollbar = ttk.Scrollbar(auth_code_frame, orient=tk.VERTICAL, 
                                      command=self.auth_code_text.yview)
        auth_scrollbar.grid(row=0, column=1, sticky=(tk.N, tk.S))
        self.auth_code_text.configure(yscrollcommand=auth_scrollbar.set)
        
        # 操作按钮
        button_frame = ttk.Frame(main_frame)
        button_frame.grid(row=8, column=0, columnspan=3, pady=10)
        
        ttk.Button(button_frame, text="复制授权码", 
                  command=self.copy_auth_code).grid(row=0, column=0, padx=5)
        ttk.Button(button_frame, text="保存到文件", 
                  command=self.save_to_file).grid(row=0, column=1, padx=5)
        ttk.Button(button_frame, text="清空", 
                  command=self.clear_all).grid(row=0, column=2, padx=5)
        
        # 状态栏
        self.status_var = tk.StringVar(value="就绪")
        status_bar = ttk.Label(main_frame, textvariable=self.status_var, 
                              relief=tk.SUNKEN, anchor=tk.W)
        status_bar.grid(row=9, column=0, columnspan=3, sticky=(tk.W, tk.E), pady=(10, 0))
        
        # 配置行权重
        main_frame.rowconfigure(7, weight=1)
        
        # 初始化界面状态
        self.on_type_changed()
    
    def on_type_changed(self):
        """授权类型改变时的处理"""
        if self.auth_type_var.get() == "通用版":
            # 通用版不需要机器码
            self.machine_code_label.grid_remove()
            self.machine_code_text.master.grid_remove()
            self.status_var.set("通用版模式：生成的授权码可在任何电脑上使用")
        else:
            # 个人版需要机器码
            self.machine_code_label.grid()
            self.machine_code_text.master.grid()
            self.status_var.set("个人版模式：需要输入用户的机器码")
    
    def use_duration(self):
        """使用当前时间加上指定天数"""
        try:
            days = self.duration_var.get()
            expire_time = datetime.now() + timedelta(days=days)
            self.expire_date_var.set(expire_time.strftime("%Y-%m-%d %H:%M:%S"))
        except Exception as e:
            messagebox.showerror("错误", f"设置过期时间失败: {str(e)}")
    
    def generate_auth_code(self):
        """生成授权码"""
        try:
            auth_type = self.auth_type_var.get()
            
            # 获取过期时间
            expire_time_str = self.expire_date_var.get().strip()
            if expire_time_str:
                try:
                    expire_time = datetime.strptime(expire_time_str, "%Y-%m-%d %H:%M:%S")
                except ValueError:
                    messagebox.showerror("错误", "过期时间格式不正确，请使用 YYYY-MM-DD HH:MM:SS 格式")
                    return
            else:
                # 使用天数计算过期时间
                days = self.duration_var.get()
                expire_time = datetime.now() + timedelta(days=days)
            
            # 检查过期时间是否在未来
            if expire_time <= datetime.now():
                messagebox.showerror("错误", "过期时间必须在当前时间之后")
                return
            
            # 根据类型生成授权码
            if auth_type == "通用版":
                auth_code = self.create_universal_auth_code(expire_time)
            else:
                # 个人版需要机器码
                machine_code = self.machine_code_text.get("1.0", tk.END).strip()
                if not machine_code:
                    messagebox.showerror("错误", "请输入机器码")
                    return
                auth_code = self.create_personal_auth_code(machine_code, expire_time)
            
            # 显示授权码
            self.auth_code_text.delete("1.0", tk.END)
            self.auth_code_text.insert("1.0", auth_code)
            
            # 更新状态
            type_desc = f"（{auth_type}）"
            self.status_var.set(f"授权码生成成功{type_desc}，过期时间: {expire_time.strftime('%Y-%m-%d %H:%M:%S')}")
            
        except Exception as e:
            messagebox.showerror("错误", f"生成授权码失败: {str(e)}")
            self.status_var.set("生成失败")
    
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
            auth_code = self.auth_code_text.get("1.0", tk.END).strip()
            if not auth_code:
                messagebox.showwarning("警告", "没有授权码可复制")
                return
            
            self.root.clipboard_clear()
            self.root.clipboard_append(auth_code)
            self.status_var.set("授权码已复制到剪贴板")
            messagebox.showinfo("成功", "授权码已复制到剪贴板")
            
        except Exception as e:
            messagebox.showerror("错误", f"复制失败: {str(e)}")
    
    def save_to_file(self):
        """保存授权码到文件"""
        try:
            auth_code = self.auth_code_text.get("1.0", tk.END).strip()
            if not auth_code:
                messagebox.showwarning("警告", "没有授权码可保存")
                return
            
            filename = filedialog.asksaveasfilename(
                title="保存授权码",
                defaultextension=".txt",
                filetypes=[("文本文件", "*.txt"), ("所有文件", "*.*")]
            )
            
            if filename:
                auth_type = self.auth_type_var.get()
                with open(filename, 'w', encoding='utf-8') as f:
                    f.write(f"XIAOFU工具箱授权码\n")
                    f.write(f"生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
                    f.write(f"授权类型: {auth_type}\n")
                    f.write(f"授权码:\n{auth_code}\n")
                
                self.status_var.set(f"授权码已保存到: {filename}")
                messagebox.showinfo("成功", f"授权码已保存到:\n{filename}")
                
        except Exception as e:
            messagebox.showerror("错误", f"保存失败: {str(e)}")
    
    def clear_all(self):
        """清空所有内容"""
        self.machine_code_text.delete("1.0", tk.END)
        self.auth_code_text.delete("1.0", tk.END)
        self.expire_date_var.set("")
        self.duration_var.set(30)
        self.auth_type_var.set("个人版")
        self.on_type_changed()
        self.status_var.set("已清空")
    
    def load_settings(self):
        """加载设置"""
        try:
            if os.path.exists("settings.json"):
                with open("settings.json", 'r', encoding='utf-8') as f:
                    settings = json.load(f)
                    self.duration_var.set(settings.get("default_duration", 30))
                    self.auth_type_var.set(settings.get("default_auth_type", "个人版"))
        except:
            pass
    
    def save_settings(self):
        """保存设置"""
        try:
            settings = {
                "default_duration": self.duration_var.get(),
                "default_auth_type": self.auth_type_var.get()
            }
            with open("settings.json", 'w', encoding='utf-8') as f:
                json.dump(settings, f, ensure_ascii=False, indent=2)
        except:
            pass
    
    def run(self):
        """运行应用程序"""
        # 窗口关闭时保存设置
        self.root.protocol("WM_DELETE_WINDOW", self.on_closing)
        self.root.mainloop()
    
    def on_closing(self):
        """窗口关闭事件"""
        self.save_settings()
        self.root.destroy()

if __name__ == "__main__":
    app = AuthCodeGenerator()
    app.run()
