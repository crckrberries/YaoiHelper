
local buildController = {}

buildController.name = "YaoiHelper/BuildController"
buildController.texture = "LoennSprites/Entities/build_controller"
buildController.depth = -10000
buildController.justification = {0.5, 0.5}
buildController.placements = {
    name = "main",
	data = {
		allow_entity_mode = true;
		unlimited = false;
		tile_limit = 10,
	}
}

return buildController
