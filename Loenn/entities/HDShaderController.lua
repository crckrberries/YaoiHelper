local HDShaderController = {}

HDShaderController.name = "YaoiHelper/HDShaderController"
HDShaderController.depth = 8998
HDShaderController.justification = {0.5, 0.5}
HDShaderController.placements = {
    name = "hd_shader_controller",
	data = {
		render_player_over = false,
		render_level_over = false,
	}
}

return HDShaderController
