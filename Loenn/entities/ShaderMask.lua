local shaderMask = {}

shaderMask.name = "YaoiHelper/ShaderMask"
shaderMask.depth = -100000
shaderMask.placements = {
	name = "main",
	data = {
		width = 16,
		height = 16,
		mask_groups = "",
		mask_image = "",
		low_res = false
	}
}

shaderMask.color = {1, 0, 1, 0.2}

return shaderMask
