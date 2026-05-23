import { defineComponent, h } from 'vue';

export const ShapesSymbolU = defineComponent({
  name: 'ShapesSymbolU',
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
        h('path', {"d": "M3.20001 2.60001V5.60001C3.20001 6.59412 4.0059 7.40001 5.00001 7.40001C5.99412 7.40001 6.80001 6.59412 6.80001 5.60001V2.60001", "fillRule": "evenodd"})
      ]
    );
  }
});
