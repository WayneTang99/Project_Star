shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec3 softened = textureLod(screen_texture, SCREEN_UV, 2.2).rgb;
    COLOR = vec4(mix(softened, vec3(0.1294, 0.1059, 0.0902), 0.72), 1.0);
}
