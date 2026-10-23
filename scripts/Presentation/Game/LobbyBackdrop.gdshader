shader_type canvas_item;
void fragment() {
    vec2 step_uv = TEXTURE_PIXEL_SIZE * 22.0;
    vec3 color = texture(TEXTURE, UV).rgb * 0.2;
    color += texture(TEXTURE, UV + vec2(step_uv.x, 0.0)).rgb * 0.1;
    color += texture(TEXTURE, UV - vec2(step_uv.x, 0.0)).rgb * 0.1;
    color += texture(TEXTURE, UV + vec2(0.0, step_uv.y)).rgb * 0.1;
    color += texture(TEXTURE, UV - vec2(0.0, step_uv.y)).rgb * 0.1;
    color += texture(TEXTURE, UV + step_uv).rgb * 0.1;
    color += texture(TEXTURE, UV - step_uv).rgb * 0.1;
    color += texture(TEXTURE, UV + vec2(step_uv.x, -step_uv.y)).rgb * 0.1;
    color += texture(TEXTURE, UV + vec2(-step_uv.x, step_uv.y)).rgb * 0.1;
    COLOR = vec4(color * 0.55, 1.0);
}
