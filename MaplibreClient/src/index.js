import './index.css'

import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap'

import 'maplibre-gl/dist/maplibre-gl.css'
import maplibregl from 'maplibre-gl'

const maplibreMap = new maplibregl.Map({
    container: 'maplibreMap',
    style: 'style.json'
});