"""生成 4 个内置工具的图标 (64x64)，白底/黑底各一份，匹配现有项目图标风格。"""
from PIL import Image, ImageDraw
import os

SIZE = 64
RADIUS = 12

WHITE_DIR = r"C:\Users\Administrator\Desktop\project1\Icons\White"
BLACK_DIR = r"C:\Users\Administrator\Desktop\project1\Icons\Black"

# 背景色 (与现有图标的色系保持一致的饱和色)
COLORS = {
    "download":    "#2F6FED",  # 蓝
    "rename":      "#E07B2B",  # 橙
    "colorpicker": "#7A4DD9",  # 紫
    "qrcode":      "#3F8F3F",  # 绿
}

def draw_download(d: ImageDraw.ImageDraw, fg: str):
    # 向下箭头 + 底盘
    # 箭头三角形 (顶点 (32,16), 左 (24,26), 右 (40,26))
    d.polygon([(32, 14), (42, 26), (36, 26), (36, 40), (28, 40), (28, 26), (22, 26)], fill=fg)
    # 底盘
    d.rectangle([(18, 44), (46, 52)], fill=fg, outline=None)

def draw_rename(d: ImageDraw.ImageDraw, fg: str):
    # 铅笔：斜向，右下角有橡皮
    # 笔身 (从 (18,46) 到 (44,20) 的斜矩形)
    d.polygon([
        (18, 46), (42, 22), (50, 30), (26, 54)
    ], fill=fg)
    # 笔尖三角形
    d.polygon([(18,46),(26,54),(20,54),(14,50)], fill=fg)
    # 笔身中分隔线 (用背景色画一条)
    d.line([(22,50),(46,26)], fill=fg, width=0)
    # 头部高亮
    d.polygon([(42,22),(50,30),(46,30),(38,22)], fill=fg)

def draw_colorpicker(d: ImageDraw.ImageDraw, fg: str):
    # 滴管：斜向 + 底部小水滴
    # 主体
    d.polygon([
        (14, 18), (40, 18), (46, 24), (46, 32), (30, 50), (24, 50), (18, 44)
    ], fill=fg)
    # 顶部把手
    d.rectangle([(12, 14), (42, 20)], fill=fg)
    # 底部水滴
    d.ellipse([(26, 44), (38, 56)], fill=fg)

def draw_qrcode(d: ImageDraw.ImageDraw, fg: str):
    # 3x3 模拟二维码风格
    d.rectangle([(12, 12), (28, 28)], outline=fg, width=3)
    d.rectangle([(16, 16), (24, 24)], fill=fg)
    d.rectangle([(36, 12), (52, 28)], outline=fg, width=3)
    d.rectangle([(40, 16), (48, 24)], fill=fg)
    d.rectangle([(12, 36), (28, 52)], outline=fg, width=3)
    d.rectangle([(16, 40), (24, 48)], fill=fg)
    # 右下角小点
    d.rectangle([(34, 34), (40, 40)], fill=fg)
    d.rectangle([(44, 34), (50, 40)], fill=fg)
    d.rectangle([(34, 44), (40, 50)], fill=fg)
    d.rectangle([(44, 44), (50, 50)], fill=fg)

DRAWERS = {
    "download": draw_download,
    "rename": draw_rename,
    "colorpicker": draw_colorpicker,
    "qrcode": draw_qrcode,
}

def make(name: str, bg: str, fg: str, out_path: str):
    img = Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))
    d = ImageDraw.Draw(img)
    # 圆角背景
    d.rounded_rectangle([(2, 2), (SIZE-3, SIZE-3)], radius=RADIUS, fill=bg)
    DRAWERS[name](d, fg)
    img.save(out_path, "PNG")
    print("wrote", out_path)

for name, bg in COLORS.items():
    # White 版本：白前景
    make(name, bg, "#FFFFFF", os.path.join(WHITE_DIR, f"{name}.png"))
    # Black 版本：深色前景 (用深灰)
    make(name, bg, "#1A1A1A", os.path.join(BLACK_DIR, f"{name}.png"))

print("done.")
