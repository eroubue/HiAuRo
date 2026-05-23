import { defineComponent, h } from 'vue';

export const ShapesSymbolI = defineComponent({
  name: 'ShapesSymbolI',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M5 7.39987L5 2.60013", "fillRule": "evenodd"})
      ]
    );
  }
});
