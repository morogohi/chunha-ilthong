# 사이트 조립: 실루엣 카드 삽입, OG 이미지·아이콘 생성
import json, html
REPO = "https://github.com/morogohi/chunha-ilthong"
sil = json.load(open('/tmp/silhouettes.json')) if __import__('os').path.exists('/tmp/silhouettes.json') else json.load(open('assets/silhouettes.json'))
json.dump(sil, open('assets/silhouettes.json','w'), ensure_ascii=False)
cards = "".join(
    f'<figure class="hero-card reveal"><svg viewBox="0 0 140 300" role="img" aria-label="{html.escape(s["label"])}">{s["svg"]}</svg>'
    f'<b><i style="background:{s["color"]}"></i>{s["name"]}</b><small>{html.escape(s["desc"])}</small></figure>' for s in sil)
t = open('index.template.html').read().replace('{{SILHOUETTES}}', cards).replace('{{REPO_URL}}', REPO)
open('index.html','w').write(t)

from PIL import Image, ImageDraw, ImageFont, ImageFilter
import math, random
F = '/usr/share/fonts/opentype/noto/'
serif = lambda s: ImageFont.truetype(F+'NotoSerifCJK-Bold.ttc' if __import__('os').path.exists(F+'NotoSerifCJK-Bold.ttc') else F+'NotoSansCJK-Bold.ttc', s, index=1)
sans = lambda s: ImageFont.truetype(F+'NotoSansCJK-Medium.ttc', s, index=1)
def ink_scene(W, H):
    im = Image.new('RGB', (W, H), (239, 232, 216)); d = ImageDraw.Draw(im, 'RGBA')
    random.seed(7)
    mx, my, mr = int(W*.76), int(H*.28), int(min(W,H)*.14)
    for r in range(mr*3, mr, -6): d.ellipse([mx-r, my-r, mx+r, my+r], fill=(255, 240, 215, 6))
    d.ellipse([mx-mr, my-mr, mx+mr, my+mr], fill=(250, 243, 230, 255))
    def n(x): return (math.sin(x*1.7)+math.sin(x*3.1+1.3)*.5+math.sin(x*7.3+2)*.25)/1.75
    for li, (base, a, amp) in enumerate([(.55, 26, .18), (.66, 44, .14), (.78, 80, .12), (.9, 160, .08)]):
        pts = [(x, H*base - H*amp*(0.6+0.5*n(x/W*4+li*3))) for x in range(0, W+8, 8)]
        d.polygon([(0, H)] + pts + [(W, H)], fill=(30, 28, 26, a))
    return im.filter(ImageFilter.GaussianBlur(0.6))
og = ink_scene(1200, 630); d = ImageDraw.Draw(og, 'RGBA')
d.rounded_rectangle([90, 190, 760, 380], radius=60, fill=(166, 58, 43, 235))
d.text((425, 285), "천하일통", font=serif(150), fill=(246, 241, 230), anchor='mm')
d.text((425, 440), "열두 성, 여섯 군웅, 그리고 당신의 기록", font=sans(38), fill=(30, 28, 26), anchor='mm')
d.text((425, 495), "수묵으로 그린 삼국 전략 시뮬레이션 · 브라우저 체험판 공개", font=sans(26), fill=(69, 65, 59), anchor='mm')
d.rectangle([1050, 500, 1130, 580], fill=(166, 58, 43)); d.text((1090, 540), "許", font=serif(56), fill=(246, 241, 230), anchor='mm')
og.save('assets/og.png', optimize=True)
icon = Image.new('RGB', (1024, 1024), (166, 58, 43)); d = ImageDraw.Draw(icon)
d.rectangle([60, 60, 964, 964], outline=(246, 241, 230), width=18)
d.text((512, 330), "天下", font=serif(300), fill=(246, 241, 230), anchor='mm')
d.text((512, 690), "一統", font=serif(300), fill=(246, 241, 230), anchor='mm')
icon.save('assets/icon-1024.png'); icon.resize((180, 180)).save('assets/icon.png')
print('built', len(t)//1024, 'KB')
