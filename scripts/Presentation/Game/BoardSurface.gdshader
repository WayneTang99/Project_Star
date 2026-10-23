shader_type canvas_item;

uniform vec4 grain_tint : source_color = vec4(1.0);
uniform float grain_opacity : hint_range(0.0, 1.0) = 0.18;

void fragment() {
    // 固定显示模板的圆角遮罩；材质参数只能改变观感，不能移动控件或输入区域。
    vec2 pixels = UV * vec2(1350.0, 720.0);
    vec2 q = abs(pixels - vec2(675.0, 360.0)) - vec2(615.0, 318.0);
    float edge = length(max(q / vec2(60.0, 42.0), vec2(0.0)));
    float mask = 1.0 - smoothstep(0.98, 1.0, edge);
    vec3 stone = texture(TEXTURE, UV * vec2(1.7, 0.95)).rgb * grain_tint.rgb;
    COLOR = vec4(stone, grain_opacity * mask);
}
