import './index.css'

import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap'

import 'ol/ol.css';
import MVT from 'ol/format/MVT';
import Map from 'ol/Map';
import View from 'ol/View';
import { Fill, Stroke, Style } from 'ol/style';
import { OSM, Vector as VectorSource, VectorTile as VectorTileSource } from 'ol/source';
import { Tile as TileLayer, Vector as VectorLayer, VectorTile as VectorTileLayer } from 'ol/layer';
import { transform } from 'ol/proj'

const vectorTileLayer = new VectorTileLayer({
	declutter: true,
	source: new VectorTileSource({
		format: new MVT(),
		url: 'https://functions.yandexcloud.net/d4esngj8sq9q03pu1f39/?x={x}&y={y}&z={z}'
	}),
	style: new Style({
        stroke: new Stroke({
            color: 'rgba(255, 0, 0, 1)',
            width: 0.5
        }),
        fill: new Fill({
            color: 'rgba(255, 0, 0, 0.095)'
        })
    })
})

const olMap = new Map({
    layers: [
        new TileLayer({
            source: new OSM()
        }),
        vectorTileLayer
    ],
    target: 'olMap',
    view: new View({
        center: transform([51, 53], 'EPSG:4326', 'EPSG:3857'),
        zoom: 6
    })
});