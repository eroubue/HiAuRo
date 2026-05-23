import { defineComponent, h } from 'vue';

export const ShapesSymbolJ = defineComponent({
  name: 'ShapesSymbolJ',
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
        h('path', {"d": "M6.24976 2.60013V6.09967C6.24976 6.09975 6.24976 6.09979 6.24976 6.09982C6.24972 6.81778 5.66767 7.39978 4.9497 7.39975C4.94967 7.39975 4.94963 7.39975 4.94956 7.39975C4.15024 7.39987 3.75024 7.09965 3.75024 7.09965", "fillRule": "evenodd"})
      ]
    );
  }
});
