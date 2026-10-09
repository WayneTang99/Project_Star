shader_type canvas_item;
render_mode unshaded;

uniform float motion_time = 0.0;
varying vec4 portrait_tint;

void vertex() {
    portrait_tint = COLOR;
}

float soft_region(vec2 uv, vec2 center, vec2 radius) {
    vec2 offset = (uv - center) / radius;
    return 1.0 - smoothstep(0.35, 1.0, dot(offset, offset));
}

void fragment() {
    float phase = motion_time * 6.2831853 / 8.0;
    // 蒙版对应圣骑士现有方形原画：保护面部与边缘，仅轻微摆动布料。
    float face = soft_region(UV, vec2(0.514, 0.250), vec2(0.20, 0.18));
    float edge = smoothstep(0.0, 0.07, min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y)));
    float cloak = soft_region(UV, vec2(0.84, 0.65), vec2(0.22, 0.25))
        + soft_region(UV, vec2(0.10, 0.76), vec2(0.15, 0.25));
    float tunic = soft_region(UV, vec2(0.46, 0.81), vec2(0.29, 0.25));
    vec2 animated_uv = UV;
    animated_uv.x += sin(phase + UV.y * 8.0) * 0.003 * cloak * edge * (1.0 - face);
    animated_uv.y += sin(phase) * 0.0016 * tunic * edge * (1.0 - face);
    vec4 base = texture(TEXTURE, clamp(animated_uv, vec2(0.0), vec2(1.0)));

    // 圣徽呼吸光与上浮光点共用八秒循环，沿用控件淡入的颜色和透明度。
    vec2 hammer = (UV - vec2(0.307, 0.195)) / vec2(0.065);
    vec2 emblem = (UV - vec2(0.414, 0.478)) / vec2(0.044);
    float radiance = exp(-dot(hammer, hammer)) + exp(-dot(emblem, emblem));
    float pulse = 0.5 + 0.5 * sin(phase * 2.0);
    vec3 light = vec3(1.0, 0.66, 0.22) * radiance * (0.05 + 0.15 * pulse);

    for (int i = 0; i < 16; i++) {
        float seed = float(i);
        float life = fract(motion_time / 8.0 + seed * 0.618034);
        float spread = fract(sin(seed * 127.1 + 3.7) * 43758.5453);
        vec2 point = vec2(0.08 + spread * 0.84 + sin(phase + seed) * 0.012, 0.92 - life * 0.83);
        vec2 distance = UV - point;
        float radius = 0.0018 + spread * 0.0012;
        float spark = exp(-dot(distance, distance) / (radius * radius));
        float halo = exp(-dot(distance, distance) / (radius * radius * 7.0)) * 0.2;
        float fade = smoothstep(0.0, 0.12, life) * (1.0 - smoothstep(0.75, 1.0, life));
        light += vec3(1.0, 0.74, 0.34) * (spark + halo) * fade * 0.65;
    }
    base.rgb += light * (1.0 - face) * base.a;
    COLOR = base * portrait_tint;
}
