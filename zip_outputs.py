import zipfile, os

src = r"D:\workbuddy_project\2026-07-15-12-35-50\outputs"
dst = r"D:\workbuddy_project\2026-07-15-12-35-50\outputs\Toolbox-Release.zip"

# 单文件交付：只打包 exe 与 config（图标/主题/AppIcon 已全部嵌入 exe）
include = ["Toolbox.exe", "Toolbox.exe.config"]

if os.path.exists(dst):
    os.remove(dst)

with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as zf:
    for name in include:
        full = os.path.join(src, name)
        if os.path.exists(full):
            zf.write(full, name)
            print("added:", name, os.path.getsize(full), "bytes")

print("ZIP created:", dst)
print("Size:", os.path.getsize(dst), "bytes")
