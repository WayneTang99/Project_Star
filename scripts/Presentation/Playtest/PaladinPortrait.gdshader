shader_type canvas_item;
render_mode unshaded;

uniform float motion_time = 0.0;
varying vec4 portrait_tint;

// 原画中的头颈、上身和锤头轮廓；蒙版边缘内收，避免背景建筑跟着人物摆动。
const vec2 HEAD[12] = {
    vec2(0.410, 0.255), vec2(0.396, 0.208), vec2(0.431, 0.141),
    vec2(0.505, 0.109), vec2(0.559, 0.112), vec2(0.607, 0.153),
    vec2(0.608, 0.222), vec2(0.587, 0.296), vec2(0.563, 0.325),
    vec2(0.574, 0.348), vec2(0.473, 0.375), vec2(0.423, 0.356)
};
const vec2 BODY[12] = {
    vec2(0.000, 0.456), vec2(0.264, 0.355), vec2(0.416, 0.325),
    vec2(0.585, 0.350), vec2(0.675, 0.244), vec2(0.741, 0.292),
    vec2(0.834, 0.322), vec2(0.946, 0.574), vec2(1.000, 0.637),
    vec2(1.000, 1.000), vec2(0.000, 1.000), vec2(0.000, 0.670)
};
const vec2 HAMMER[12] = {
    vec2(0.125, 0.218), vec2(0.153, 0.094), vec2(0.218, 0.057),
    vec2(0.276, 0.035), vec2(0.334, 0.052), vec2(0.373, 0.109),
    vec2(0.418, 0.142), vec2(0.426, 0.230), vec2(0.381, 0.308),
    vec2(0.280, 0.349), vec2(0.238, 0.288), vec2(0.169, 0.260)
};

void vertex() {
    portrait_tint = COLOR;
}

float soft_region(vec2 uv, vec2 center, vec2 radius) {
    vec2 offset = (uv - center) / radius;
    return 1.0 - smoothstep(0.35, 1.0, dot(offset, offset));
}

float silhouette(vec2 uv, vec2 outline[12]) {
    float distance = 1.0;
    bool inside = false;
    for (int i = 0; i < 12; i++) {
        vec2 a = outline[i];
        vec2 b = outline[(i + 1) % 12];
        vec2 segment = b - a;
        vec2 nearest = a + segment * clamp(dot(uv - a, segment) / dot(segment, segment), 0.0, 1.0);
        distance = min(distance, length(uv - nearest));
        if ((a.y > uv.y) != (b.y > uv.y)) {
            if (uv.x < a.x + (uv.y - a.y) * (b.x - a.x) / (b.y - a.y)) inside = !inside;
        }
    }
    return inside ? smoothstep(0.0, 0.022, distance) : 0.0;
}

vec2 rotate_about(vec2 uv, vec2 pivot, float angle) {
    vec2 local = uv - pivot;
    float c = cos(angle);
    float s = sin(angle);
    return pivot + vec2(c * local.x - s * local.y, s * local.x + c * local.y);
}

// 圣光围绕原画中的发光部位扩散，短光芒与柔和光晕共用循环相位。
float holy_light(vec2 uv, vec2 center, float radius, float phase) {
    vec2 local = (uv - center) / radius;
    float distance = length(local);
    float pulse = 0.5 + 0.5 * sin(phase * 2.0);
    float halo = exp(-dot(local, local) * 0.65) * (0.08 + 0.18 * pulse);
    float core = exp(-dot(local, local) * 5.0) * (0.10 + 0.20 * pulse);
    float rays = pow(max(0.0, cos(atan(local.y, local.x) * 8.0 - phase * 2.0)), 12.0);
    rays *= exp(-distance * 1.8) * smoothstep(0.2, 0.5, distance) * (0.08 + 0.16 * pulse);
    return halo + core + rays;
}

void fragment() {
    float phase = motion_time * 6.2831853 / 8.0;
    float breath = sin(phase * 2.0);
    float sway = sin(phase);
    float edge = smoothstep(0.0, 0.035, min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y)));
    float head = silhouette(UV, HEAD);
    float body = silhouette(UV, BODY) * (1.0 - head);
    vec2 shaft = vec2(0.116, 0.754) - vec2(0.260, 0.310);
    float along = clamp(dot(UV - vec2(0.260, 0.310), shaft) / dot(shaft, shaft), 0.0, 1.0);
    float handle = 1.0 - smoothstep(0.028, 0.044, length(UV - (vec2(0.260, 0.310) + shaft * along)));
    float grip = soft_region(UV, vec2(0.207, 0.443), vec2(0.100, 0.095));
    float weapon = max(silhouette(UV, HAMMER), max(handle, grip)) * (1.0 - head);

    // 四秒呼吸、八秒重心轻摆；上身从腰部起伏，头部以颈根为轴整体移动。
    float upper = 1.0 - smoothstep(0.55, 0.98, UV.y);
    vec2 torso_offset = vec2(sway * 0.0045, breath * 0.0065) * upper;
    torso_offset.x += (UV.x - 0.5) * breath * 0.009 * upper;
    vec2 animated_uv = UV - torso_offset * body;
    vec2 head_uv = rotate_about(UV - vec2(sway * 0.003, breath * 0.0045), vec2(0.500, 0.355), sway * 0.018);
    animated_uv = mix(animated_uv, head_uv, head);

    // 手与整把锤共用位移和旋转，避免锤头独自扭动或脱离握柄。
    vec2 weapon_uv = rotate_about(UV - vec2(sway * 0.003, breath * 0.0065), vec2(0.215, 0.465), sway * 0.012);
    animated_uv = mix(animated_uv, weapon_uv, weapon);

    // 发梢和布料延迟摆动，面部与金属护甲不参与柔性波动。
    float hair = soft_region(UV, vec2(0.515, 0.160), vec2(0.104, 0.060)) * head;
    animated_uv.x += sin(phase * 2.0 + UV.y * 12.0) * 0.0025 * hair;
    float cloak = soft_region(UV, vec2(0.859, 0.657), vec2(0.185, 0.129))
        + soft_region(UV, vec2(0.075, 0.878), vec2(0.170, 0.200));
    float tunic = soft_region(UV, vec2(0.510, 0.940), vec2(0.315, 0.205));
    float cloth = (1.0 - max(head, weapon)) * body;
    animated_uv.x += (sin(phase + UV.y * 9.0) * 0.012 * cloak
        + sin(phase * 2.0 + UV.y * 7.0) * 0.005 * tunic) * cloth;
    animated_uv.y += sin(phase + UV.x * 8.0) * 0.004 * cloak * cloth;
    animated_uv = mix(UV, animated_uv, edge);
    vec4 base = texture(TEXTURE, clamp(animated_uv, vec2(0.0), vec2(1.0)));

    // 光源跟随握锤与胸肩的位移，保持与原画圣徽对齐。
    float radiance = holy_light(weapon_uv, vec2(0.307, 0.195), 0.072, phase)
        + holy_light(UV - torso_offset, vec2(0.414, 0.478), 0.051, phase + 0.4);
    vec3 light = vec3(1.0, 0.72, 0.30) * radiance;
    for (int i = 0; i < 12; i++) {
        float seed = float(i);
        float life = fract(motion_time / 8.0 + seed * 0.618034);
        float spread = fract(sin(seed * 127.1 + 3.7) * 43758.5453);
        vec2 point = vec2(0.08 + spread * 0.84 + sin(phase + seed) * 0.014, 0.93 - life * 0.82);
        vec2 local = UV - point;
        float radius = 0.0018 + spread * 0.0012;
        float spark = exp(-dot(local, local) / (radius * radius));
        float halo = exp(-dot(local, local) / (radius * radius * 10.0)) * 0.20;
        float fade = smoothstep(0.0, 0.12, life) * (1.0 - smoothstep(0.78, 1.0, life));
        light += vec3(1.0, 0.80, 0.42) * (spark + halo) * fade * 0.65;
    }
    // 柔和提亮保留盔甲纹理，避开面部和卡面边界，并沿用控件淡入透明度。
    light *= (1.0 - head) * edge * base.a;
    base.rgb += max(vec3(0.0), vec3(1.0) - base.rgb) * clamp(light, vec3(0.0), vec3(1.0));
    COLOR = base * portrait_tint;
}
