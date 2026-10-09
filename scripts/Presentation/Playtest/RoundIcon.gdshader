shader_type canvas_item;
void fragment() {
    vec4 pixel = texture(TEXTURE, UV);
    pixel.a *= 1.0 - smoothstep(0.475, 0.5, length(UV - vec2(0.5)));
    COLOR = pixel * COLOR;
}
