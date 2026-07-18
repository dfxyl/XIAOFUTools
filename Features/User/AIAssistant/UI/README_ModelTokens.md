# 模型特殊标记过滤说明

## 🎯 功能

自动过滤AI模型输出中的特殊标记,让用户看到干净的内容。

## 🔖 支持的标记

### 1. **Box标记** (DeepSeek等)
```
<|begin_of_box|>
<|end_of_box|>
```

### 2. **Text标记** (Llama等)
```
<|begin_of_text|>
<|end_of_text|>
```

### 3. **Header标记** (Llama 3)
```
<|start_header_id|>
<|end_header_id|>
```

### 4. **EOT标记** (End of Turn)
```
<|eot_id|>
```

### 5. **ChatML标记** (GPT系列)
```
<|im_start|>
<|im_end|>
```

### 6. **角色标记**
```
<|system|>
