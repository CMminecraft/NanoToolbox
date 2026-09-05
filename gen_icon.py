from PIL import Image, ImageDraw
import os

# 配置
bg_color = (0x29, 0x62, 0xFF)  # 主题主色 #2962FF
icon_path = r"C:\Users\Administrator\Desktop\图标\游戏图标包-v1.4-PNG\无间距\64像素\白色\2-物品\工具箱.png"
out_path = r"C:\Users\Administrator\Desktop\project1\AppIcon.ico"

# 创建 256x256 蓝色圆角背景
size = 256
img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(img)

radius = 56  # 圆角半径
draw.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=bg_color)

# 加载工具箱图标并缩放到 160x160
icon = Image.open(icon_path).convert("RGBA")
# 原图标 64x64，等比放大
icon = icon.resize((160, 160), Image.Resampling.LANCZOS)

# 居中粘贴
x = (size - icon.width) // 2
y = (size - icon.height) // 2
img.paste(icon, (x, y), icon)

# 生成 ICO 所需尺寸
sizes = [(256, 256), (64, 64), (32, 32), (16, 16)]
frames = []
for s in sizes:
    f = img.resize(s, Image.Resampling.LANCZOS)
    frames.append(f)

# 保存 ICO
img.save(out_path, format="ICO", sizes=sizes)
print(f"Saved: {out_path}")
